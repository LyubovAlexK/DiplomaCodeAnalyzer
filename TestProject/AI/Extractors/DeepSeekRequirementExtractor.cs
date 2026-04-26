using Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AI.Extractors
{
    public class DeepSeekRequirementExtractor : IRequirementExtractor
    {
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _model;

        public DeepSeekRequirementExtractor(string apiKey)
        {
            _apiKey = apiKey;
            _endpoint = "https://openrouter.ai/api/v1";
            _model = "nousresearch/hermes-3-llama-3.1-405b:free";
        }

        public async Task<ProjectSpecification> ExtractAsync(string pdfPath)
        {
            // 1. Извлекаем текст из PDF (PdfPig)
            string text = ExtractTextFromPdf(pdfPath);

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Не удалось извлечь текст из PDF.");
            }

            // 2. Отправляем в DeepSeek через OpenRouter
            var requirements = await AnalyzeWithDeepSeek(text);

            // 3. Формируем результат
            return new ProjectSpecification
            {
                Title = Path.GetFileNameWithoutExtension(pdfPath),
                ExtractionType = "DeepSeek",
                Requirements = requirements,
                CreatedBy = 1
            };
        }

        //Отправляем текст ТЗ в DeepSeek и получает структурированный список требований.
        private async Task<List<Requirement>> AnalyzeWithDeepSeek(string text)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            var systemPrompt = @"
                Ты — анализатор технических заданий для системы проверки кода стажеров.
                Твоя задача: извлечь из ТЗ ВСЕ требования и вернуть их в JSON-формате.

                ПРАВИЛА:
                1. Типы требований:
                   - Functional: что программа должна делать (действия, функции, поведение)
                   - Architectural: как должна быть устроена архитектура (запреты зависимостей, структура)
                   - Metric: критерии качества (сложность, стабильность, удобство)

                2. Для КАЖДОГО требования укажи:
                   - requirementType: ""Functional"" / ""Architectural"" / ""Metric""
                   - category: категория (Бизнес-логика, UI, Изоляция слоев, Качество кода и т.д.)
                   - title: краткое название (до 120 символов)
                   - description: полный текст требования из ТЗ
                   - severity: ""Fatal"" (обязательно) / ""Major"" (важно) / ""Minor"" (желательно)
                   - acceptanceCriteria: массив из 1 строки с формулировкой требования

                3. Формат ответа: ТОЛЬКО JSON-массив.
                   Пример: [{""requirementType"": ""Functional"", ""category"": ""UI"", ""title"": ""Добавление товара"", ""description"": ""...""}]";

            var requestBody = new
            {
                model = _model,
                messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Извлеки требования из следующего ТЗ:\n\n{text}" }
            },
                temperature = 0.1,
                max_tokens = 4096
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

            //Первая попытка
            var response = await httpClient.PostAsync($"{_endpoint}/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Ошибка API: {response.StatusCode}\n{responseBody}");
            }

            var parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var aiMessage = parsedResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            //Первая попытка парсинга JSON
            var requirements = ParseRequirements(aiMessage!);

            //Если есть ошибки - еще одна попытка
            if (requirements.Count == 0)
            {
                requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "Верни ТОЛЬКО JSON-массив требований. Исправь синтаксис." },
                        new { role = "user", content = $"Твой предыдущий ответ содержал ошибку. Исправь и верни чистый JSON:\n{aiMessage}" }
                    },
                    temperature = 0.1,
                    max_tokens = 4096
                };

                jsonRequest = JsonSerializer.Serialize(requestBody);
                content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

                response = await httpClient.PostAsync($"{_endpoint}/chat/completions", content);
                responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    parsedResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    aiMessage = parsedResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    requirements = ParseRequirements(aiMessage!);
                }
            }

            //Присваиваем ID
            int func = 1, arch = 1, metric = 1;
            foreach (var req in requirements)
            {
                req.CreatedAt = DateTime.Now;
                req.Title = req.RequirementType switch
                {
                    "Functional" => $"FUNC-{func++:D2}: {req.Title}",
                    "Architectural" => $"ARCH-{arch++:D2}: {req.Title}",
                    "Metric" => $"QUAL-{metric++:D2}: {req.Title}",
                    _ => req.Title
                };
            }

            return requirements;
        }
        //Парсим JSON-ответ от ИИ в список требований
        private List<Requirement> ParseRequirements(string json)
        {
            try
            {
                //Убираем возможный текст до и после JSON
                json = json.Trim();
                if (json.StartsWith("```json"))
                    json = json[7..];
                if (json.StartsWith("```"))
                    json = json[3..];
                if (json.EndsWith("```"))
                    json = json[..^3];
                json = json.Trim();

                return JsonSerializer.Deserialize<List<Requirement>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Requirement>();
            }
            catch (JsonException)
            {
                return new List<Requirement>();
            }
        }

        //Извлекаем текст из PGF
        private string ExtractTextFromPdf(string path)
        {
            using var document = PdfDocument.Open(path);
            return string.Join("\n", document.GetPages().Select(p => p.Text));
        }

    }

}
