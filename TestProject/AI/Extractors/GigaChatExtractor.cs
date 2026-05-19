using Core.Models;
using Core.Services;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AI.Extractors;

public class GigaChatExtractor : IRequirementExtractor
{
    private readonly string _authKey;
    private readonly string _scope;
    private readonly DictionaryService _dict;
    private string? _accessToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    private const string OAuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    private const string ApiUrl = "https://gigachat.devices.sberbank.ru/api/v1";

    public GigaChatExtractor(string authKey, DictionaryService dict, string scope = "GIGACHAT_API_PERS")
    {
        _authKey = authKey;
        _dict = dict;
        _scope = scope;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        await EnsureTokenAsync();
        return _accessToken!;
    }

    public async Task<ProjectSpecification> ExtractAsync(string filePath, int specificationId = 0)
    {
        string text = TextExtractor.ExtractText(filePath);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Не удалось извлечь текст из документа.");

        await EnsureTokenAsync();

        // Загружаем промпт из БД
        var prompt = await _dict.GetPromptAsync(specificationId, "Extraction");
        string systemPrompt = prompt?.SystemPrompt ?? GetDefaultSystemPrompt();
        string userPrompt = prompt?.UserPromptTemplate != null
            ? string.Format(prompt.UserPromptTemplate, text)
            : $"Извлеки требования из следующего ТЗ:\n\n{text}";

        var requirements = await AnalyzeWithGigaChat(systemPrompt, userPrompt);

        return new ProjectSpecification
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            ExtractionType = "GigaChat",
            Requirements = requirements,
            CreatedBy = 1
        };
    }

    private static string GetDefaultSystemPrompt()
    {
        return @"Ты — анализатор технических заданий. Извлеки из текста все требования и верни их в JSON-формате.
ПРАВИЛА:
1. Типы требований: Functional (действия, функции), Architectural (архитектура), Metric (качество).
2. Для каждого укажи: requirementType, category, title, description, severity (Fatal/Major/Minor), acceptanceCriteria (массив строк).
3. Ответ: ТОЛЬКО JSON-массив.";
    }

    private async Task EnsureTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiresAt)
            return;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            CheckCertificateRevocationList = false
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("RqUID", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _authKey);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("scope", _scope)
        });

        var response = await client.PostAsync(OAuthUrl, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ошибка получения токена: {response.StatusCode}\n{responseBody}");

        var json = JsonDocument.Parse(responseBody);
        _accessToken = json.RootElement.GetProperty("access_token").GetString();
        _tokenExpiresAt = DateTime.Now.AddMinutes(25);
    }

    private async Task<List<Requirement>> AnalyzeWithGigaChat(string systemPrompt, string userPrompt)
    {
        Console.WriteLine("Получаем токен...");
        await EnsureTokenAsync();
        Console.WriteLine("Токен получен\n");

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            CheckCertificateRevocationList = false
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var requestBody = new
        {
            model = "GigaChat",
            messages = new[]
            {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        },
            temperature = 0.1,
            max_tokens = 65536
        };

        var jsonRequest = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        Console.WriteLine("Отправляем запрос к GigaChat...");
        Console.WriteLine($"Длина промпта: {userPrompt.Length} символов\n");

        var startTime = DateTime.Now;
        var response = await client.PostAsync($"{ApiUrl}/chat/completions", content);
        var elapsed = (DateTime.Now - startTime).TotalSeconds;

        Console.WriteLine($"⏱Ответ получен за {elapsed:F1} сек");

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Ошибка API: {response.StatusCode}");
            throw new InvalidOperationException($"Ошибка API: {response.StatusCode}\n{responseBody}");
        }

        Console.WriteLine("Ответ успешный\n");

        var parsedResponse = JsonDocument.Parse(responseBody);
        var choices = parsedResponse.RootElement.GetProperty("choices");

        if (choices.GetArrayLength() == 0)
        {
            Console.WriteLine("Модель вернула пустой ответ");
            return new List<Requirement>();
        }

        var aiMessage = choices[0].GetProperty("message").GetProperty("content").GetString();
        Console.WriteLine($"Длина ответа модели: {aiMessage?.Length ?? 0} символов");

        Console.WriteLine("=== НАЧАЛО ОТВЕТА ===");
        Console.WriteLine(aiMessage?[..Math.Min(300, aiMessage.Length)] ?? "null");
        Console.WriteLine("=== КОНЕЦ ОТВЕТА ===");
        Console.WriteLine(aiMessage?[^Math.Min(300, aiMessage.Length)..] ?? "null");
        Console.WriteLine();

        Console.WriteLine("Парсим JSON...");
        var requirements = ParseRequirements(aiMessage!);
        Console.WriteLine($"Распарсено требований: {requirements.Count}\n");

        return requirements;
    }

    private List<Requirement> ParseRequirements(string json)
    {
        try
        {
            json = json.Trim();
            if (json.StartsWith("```json")) json = json[7..];
            if (json.StartsWith("```")) json = json[3..];
            if (json.EndsWith("```")) json = json[..^3];

            // Очистка
            json = json.Replace("\r\n", "\n").Replace("\r", "\n");
            json = json.Replace("?", "");
            json = json.Replace('\u3000', ' ');
            json = json.Replace('\u00A0', ' ');
            json = Regex.Replace(json, @"  +", " ");
            json = Regex.Replace(json, @"[\u0000-\u001F\u200B\u200C\u200D\uFEFF]", "");
            json = json.Trim();

            // Сохраняем для диагностики
            File.WriteAllText(@"..\..\..\..\gigachat_response.json", json);

            // Простой подход: используем JsonConvert с настройками
            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    // Пропускаем ошибки в отдельных элементах
                    args.ErrorContext.Handled = true;
                }
            };

            var rawRequirements = JsonConvert.DeserializeObject<List<RawRequirement>>(json, settings);
            if (rawRequirements == null)
            {
                Console.WriteLine("DeserializeObject вернул null");
                return new List<Requirement>();
            }

            // Убираем null-элементы
            rawRequirements = rawRequirements.Where(r => r != null).ToList();
            Console.WriteLine($"Десериализовано: {rawRequirements.Count} объектов\n");

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
                    AcceptanceCriteria = raw.AcceptanceCriteria != null
                        ? JsonConvert.SerializeObject(raw.AcceptanceCriteria) : null,
                    Threshold = raw.Threshold,
                    MetricName = raw.MetricName,
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