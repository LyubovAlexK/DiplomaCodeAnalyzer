using Core.Data;
using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzers.Services
{
    public class RoslynResultService
    {
        private readonly AppDbContext _db;

        public RoslynResultService(AppDbContext db)
        {
            _db = db;
        }

        //Сохраняем результаты Roysln-анализа в AuditVerdicts
        public async Task SaveResultsAsync(int sessionId, CodeAnalysisResult result)
        {
            foreach (var method in result.Methods)
            {
                AuditVerdict? verdict = null;

                if (method.CyclomaticComplexity > 10)
                {
                    verdict = new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = null,
                        IsPassed = false,
                        Reason = $"Цикломатическая сложность: {method.CyclomaticComplexity} (порог: 10). Метод: {method.ClassName}.{method.MethodName}",
                        CodeLocation = $"{method.FilePath}:{method.ClassName}.{method.MethodName}",
                        AiModel = "Roslyn",
                        Confidence = null
                    };
                }
                else if (method.ExecutableLines > 20)
                {
                    verdict = new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = null,
                        IsPassed = false,
                        Reason = $"Исполняемых строк: {method.ExecutableLines} (порог: 20). Метод: {method.ClassName}.{method.MethodName}",
                        CodeLocation = $"{method.FilePath}:{method.ClassName}.{method.MethodName}",
                        AiModel = "Roslyn",
                        Confidence = null
                    };
                }
                else if (method.NestingDepth > 4)
                {
                    verdict = new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = null,
                        IsPassed = false,
                        Reason = $"Глубина вложенности: {method.NestingDepth} (порог: 4). Метод: {method.ClassName}.{method.MethodName}",
                        CodeLocation = $"{method.FilePath}:{method.ClassName}.{method.MethodName}",
                        AiModel = "Roslyn",
                        Confidence = null
                    };
                }

                if (verdict != null)
                {
                    _db.AuditVerdicts.Add(verdict);
                    await _db.SaveChangesAsync();
                }
            }
        }
    
        //Создаем сессии
        public async Task<int> CreateSessionAsync(int traineeId, int projectId, int specificationId, bool isAiAvailable)
        {
            var session = new SessionAnalysis
            {
                TraineeId = traineeId,
                ProjectId = projectId,
                SpecificationId = specificationId,
                StartTime = DateTime.Now,
                Status = "InProgress",
                IsAiAvailable = isAiAvailable,
                IsArchived = false
            };

            _db.SessionAnalysis.Add(session);
            await _db.SaveChangesAsync();
            return session.SessionId;
        }

        //Обновляем статус сессии
        public async Task UpdateSessionAsync(int sessionId, CodeAnalysisResult result)
        {
            var session = await _db.SessionAnalysis.FindAsync(sessionId);
            if (session != null)
            {
                session.EndTime = DateTime.Now;
                session.Status = "Completed";
                await _db.SaveChangesAsync();
            }
        }
    }
}
