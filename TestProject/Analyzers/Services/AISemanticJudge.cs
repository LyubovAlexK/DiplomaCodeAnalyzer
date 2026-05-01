using Azure.Core;
using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Analyzers.Services
{
    public class AISemanticJudge
    {
        private readonly AppDbContext _db;
        private readonly string _accessToken;

        private const string ApiUrl = "https://gigachat.devices.sberbank.ru/api/v1";

        public AISemanticJudge(AppDbContext db, string accessToken)
        {
            _db = db;
            _accessToken = accessToken;
        }

        //Проверка всех функциональных требований в рамках сессии
        public async Task JudgeAsync(int sessionId, int specificationId, string traineeCode)
        {
            //Загрузка требований
            var requirements = await _db.Requirements
                .Where(r => r.SpecificationId == specificationId && r.RequirementType == "Functional")
                .ToListAsync();

            if (!requirements.Any())
            {
                Console.WriteLine("Нет Functional-требований для проверки.");
                return;
            }

            //Загрузка промпта
            var prompt = await _db.PromptTemplates
                .FirstOrDefaultAsync(p => p.SpecificationId == specificationId && p.PromptType == "Judgment" && p.IsActive);

            if (prompt == null)
            {
                throw new InvalidOperationException($"Не найден Judgment-промпт для SpecificationId= {specificationId}");
            }

            string systemPrompt = prompt.SystemPrompt;
            string userPromptTemplate = prompt.UserPromptTemplate;
            
            int passed = 0;
            int failed = 0;

            foreach (var req in requirements)
            {
                try
                {
                    var userPrompt = string.Format(userPromptTemplate, req.Description, traineeCode);
                    var verdict = await SendToGigaChat(systemPrompt, userPrompt);

                    _db.AuditVerdicts.Add(new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = req.RequirementId,
                        IsPassed = verdict.IsPassed,
                        Reason = verdict.Reason,
                        Confidence = verdict.Confidence,
                        AiModel = "GigaChat"
                    });
                    await _db.SaveChangesAsync();

                    if (verdict.IsPassed) passed++;
                    else failed++;
                }
                catch (Exception ex)
                {
                    _db.AuditVerdicts.Add(new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = req.RequirementId,
                        IsPassed = false,
                        Reason = $"Ошибка AI: {ex.Message}",
                        Confidence = 0,
                        AiModel = "GigaChat"
                    });
                    await _db.SaveChangesAsync();
                    failed++;
                }
            }

            //Обновление процента в сессии
            var session = await _db.SessionAnalysis.FindAsync(sessionId);
            if (session != null && requirements.Count > 0)
            {
                session.OverallMatchPercent = (decimal)passed / requirements.Count * 100;
                await _db.SaveChangesAsync();
            }

            Console.WriteLine($"AI Judge: {passed} пройдено, {failed} не пройдено из {requirements.Count}");
        }

        private async Task<(bool IsPassed, string Reason, decimal Confidence)> SendToGigaChat(string systemPrompt, string userPrompt)
        {

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var requestBody = new 
            {
                model = "GigaChat",
                messages = new[]
                {
                    new {role = "system", content = systemPrompt},
                    new {role = "user", content = userPrompt}
                },
                temperature = 0.1,
                max_tokens = 2048
            };

            var json = JsonSerializer.Serialize(requestBody);
            var response = await client.PostAsync($"{ApiUrl}/chat/completions", new StringContent(json, Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Ошибка GigaChat: {response.StatusCode}");

            var parsed = JsonDocument.Parse(body);
            var content_text = parsed.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            //Перевод в JSON вердикт
            var verdict = JsonSerializer.Deserialize<JsonElement>(content_text);
            return (
                verdict.GetProperty("IsPassed").GetBoolean(),
                verdict.GetProperty("Reason").GetString() ?? "",
                verdict.GetProperty("Confidence").GetDecimal()
            );
        }
    }
}
