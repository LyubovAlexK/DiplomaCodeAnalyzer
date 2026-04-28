using Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using static System.Net.Mime.MediaTypeNames;

namespace AI.Extractors
{
    public class GigaChatExtractor : IRequirementExtractor
    {
        private readonly string _authKey;
        private readonly string _scope;
        private string? _accessToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        private const string OAuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
        private const string ApiUrl = "https://gigachat.devices.sberbank.ru/api/v1";

        public GigaChatExtractor(string authKey, string scope = "GIGACHAT_API_PERS")
        {
            _authKey = authKey;
            _scope = scope;
        }

        public async Task<ProjectSpecification> ExtractAsync(string pdfPath)
        {
            //Извлекаем текст из PDF
            string text = ExtractTextFromPdf(pdfPath);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Не удалось извлечь текст из PDF.");

            //Получаем токен
            await EnsureTokenAsync();

            //Отправляем в GigaChat
            var requirements = await AnalyzeWithGigaChat(text);

            //Формируем результат
            return new ProjectSpecification
            {
                Title = Path.GetFileNameWithoutExtension(pdfPath),
                ExtractionType = "GigaChat",
                Requirements = requirements,
                CreatedBy = 1
            };
        }

        private async Task EnsureTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiresAt)
                return;

            var handler = new HttpClientHandler
            {
                //Обход проверки SSL (только для GigaChat API)
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                //Принудительный TLS 1.2
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                //Явно включаем все протоколы
                CheckCertificateRevocationList = false
            };

            using var client = new HttpClient(handler);

            // Таймаут 30 секунд
            client.Timeout = TimeSpan.FromSeconds(30);

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("RqUID", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", _authKey);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("scope", _scope)
            });

            try
            {
                var response = await client.PostAsync(OAuthUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Ошибка получения токена: {response.StatusCode}\n{responseBody}");

                var json = JsonDocument.Parse(responseBody);
                _accessToken = json.RootElement.GetProperty("access_token").GetString();
                _tokenExpiresAt = DateTime.Now.AddMinutes(25);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Не удалось подключиться к GigaChat API.\n" +
                    $"Проверьте:\n" +
                    $"1. Доступен ли сайт https://gigachat.devices.sberbank.ru в браузере\n" +
                    $"2. Не блокирует ли корпоративный брандмауэр порт 9443\n" +
                    $"3. Внутренняя ошибка: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        //Отправка ТЗ в GigaChat

        private async Task<List<Requirement>> AnalyzeWithGigaChat(string text)
        {
            await EnsureTokenAsync();

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                CheckCertificateRevocationList = false
            };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);

            var systemPrompt = @"
Ты — анализатор технических заданий. Извлеки из текста все требования и верни их в JSON-формате.

ПРАВИЛА:
1. Типы требований:
   - Functional: что программа должна делать (действия, функции, поведение)
   - Architectural: как должна быть устроена архитектура (запреты зависимостей, структура)
   - Metric: критерии качества (сложность, стабильность, удобство)

2. Для КАЖДОГО требования укажи:
   - requirementType: ""Functional"" / ""Architectural"" / ""Metric""
   - category: категория (Бизнес-логика, UI, Изоляция слоев, Качество кода)
   - title: краткое название (до 120 символов)
   - description: полный текст требования из ТЗ
   - severity: ""Fatal"" / ""Major"" / ""Minor""
   - acceptanceCriteria: массив из 1 строки с формулировкой

3. Формат ответа: ТОЛЬКО JSON-массив.
   Пример: [{""requirementType"": ""Functional"", ""category"": ""UI"", ""title"": ""Добавление товара"", ""description"": ""Пользователь должен иметь возможность добавить товар в корзину"", ""severity"": ""Major"", ""acceptanceCriteria"": [""Товар добавляется в корзину""]}]";

            var requestBody = new
            {
                model = "GigaChat",
                messages = new[]
                {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"Извлеки требования из следующего ТЗ:\n\n{text}" }
        },
                temperature = 0.1,
                max_tokens = 4096
            };

            var jsonRequest = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{ApiUrl}/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Ошибка API: {response.StatusCode}\n{responseBody}");
            }

            // ДИАГНОСТИКА: показываем сырой ответ
            Console.WriteLine("=== СЫРОЙ ОТВЕТ GIGACHAT ===");
            Console.WriteLine(responseBody.Length > 2000 ? responseBody[..2000] + "..." : responseBody);
            Console.WriteLine("=== КОНЕЦ СЫРОГО ОТВЕТА ===\n");

            var parsedResponse = JsonDocument.Parse(responseBody);
            var choices = parsedResponse.RootElement.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
            {
                Console.WriteLine("ОШИБКА: choices пустой");
                return new List<Requirement>();
            }

            var aiMessage = choices[0].GetProperty("message").GetProperty("content").GetString();

            Console.WriteLine("=== СООБЩЕНИЕ МОДЕЛИ ===");
            Console.WriteLine(aiMessage?.Length > 500 ? aiMessage[..500] + "..." : aiMessage ?? "null");
            Console.WriteLine("=== КОНЕЦ СООБЩЕНИЯ ===\n");

            return ParseRequirements(aiMessage!);
        }

        private HttpMessageHandler CreateHandler()
        {
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    //Принимаем сертификаты Минцифры
                    if (cert?.Issuer.Contains("Минцифры") == true ||
                        cert?.Issuer.Contains("Ministry") == true ||
                        cert?.Issuer.Contains("Russian") == true)
                        return true;

                    //Принимаем любые сертификаты (только для теста)
                    return true;
                }
            };
        }

        //Перевод JSON ответа от GigaChat в список требований

        private List<Requirement> ParseRequirements(string json)
        {
            try
            {
                json = json.Trim();
                if (json.StartsWith("```json")) json = json[7..];
                if (json.StartsWith("```")) json = json[3..];
                if (json.EndsWith("```")) json = json[..^3];
                json = json.Trim();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    //Игнорируем проблемы с \r\n внутри строк
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var rawRequirements = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RawRequirement>>(json);

                if (rawRequirements == null)
                    return new List<Requirement>();

                //Присваиваем ID
                var requirements = new List<Requirement>();
                int func = 1, arch = 1, metric = 1;
                foreach (var raw in rawRequirements)
                {
                    var req = new Requirement
                    {
                        RequirementType = raw.RequirementType ?? "Functional",
                        Category = raw.Category,
                        Title = raw.Title ?? "Без названия",
                        Description = raw.Description,
                        Severity = raw.Severity ?? "Major",
                        // Массив → строка JSON
                        AcceptanceCriteria = raw.AcceptanceCriteria != null
                            ? Newtonsoft.Json.JsonConvert.SerializeObject(raw.AcceptanceCriteria)
                            : null,
                        ExpectedBehavior = null,
                        Threshold = raw.Threshold,
                        MetricName = raw.MetricName,
                        TargetNamespace = raw.TargetNamespace,
                        ForbiddenDependency = raw.ForbiddenDependency,
                        CreatedAt = DateTime.Now
                    };

                    req.Title = req.RequirementType switch
                    {
                        "Functional" => $"FUNC-{func++:D2}: {req.Title}",
                        "Architectural" => $"ARCH-{arch++:D2}: {req.Title}",
                        "Metric" => $"QUAL-{metric++:D2}: {req.Title}",
                        _ => req.Title
                    };

                    requirements.Add(req);
                }

                return requirements;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА ПАРСИНГА: {ex.Message}");
                return new List<Requirement>();
            }
        }

        private string ExtractTextFromPdf(string path)
        {
            using var document = PdfDocument.Open(path);
            var rawText = string.Join("\n", document.GetPages().Select(p => p.Text));

            return NormalizeText(rawText);
        }

        //Нормализация - восставновление пробелов в тексте
        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            text = System.Text.RegularExpressions.Regex.Replace(text, @"([а-яёa-z])([A-ZА-ЯЁ<])", "$1 $2");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"([.:?!])([А-ЯЁA-Zа-яёa-z])", "$1 $2");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\d)([А-ЯЁA-Zа-яёa-z])", "$1 $2");
            text = System.Text.RegularExpressions.Regex.Replace(text, @",(\S)", ", $1");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        private class RawRequirement
        {
            public string? RequirementType { get; set; }
            public string? Category { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? Severity { get; set; }
            public List<string>? AcceptanceCriteria { get; set; }
            public decimal? Threshold { get; set; }
            public string? MetricName { get; set; }
            public string? TargetNamespace { get; set; }
            public string? ForbiddenDependency { get; set; }
        }
    }
}
