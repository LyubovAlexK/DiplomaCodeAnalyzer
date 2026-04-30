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
            var verdicts = new List<AuditVerdict>();

            foreach (var method in result.Methods)
            {
                if (method.CyclomaticComplexity > 10)
                {
                    verdicts.Add(new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = 0,
                        IsPassed = false,
                        Reason = $"Цикломатическая сложность: {method.CyclomaticComplexity} (порог: 10). Метод: {method.ClassName}.{method.MethodName}",
                        CodeLocation = $"{method.FilePath}:{method.ClassName}.{method.MethodName}",
                        AiModel = "Roysln",
                        Confidence = null
                    });
                }

                if (method.ExecutableLines > 20)
                {
                    verdicts.Add(new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = 0,
                        IsPassed = false,
                        Reason = $"Исполняемых строк: {method.ExecutableLines} (порог: 20). Метод: {method.ClassName}.{method.MethodName}",
                        CodeLocation = $"{method.FilePath}:{method.ClassName}.{method.MethodName}",
                        AiModel = "Roysln",
                        Confidence = null
                    });
                }

                if (method.NestingDepth > 4)
                {
                    verdicts.Add(new AuditVerdict
                    {
                        SessionId = sessionId,
                        RequirementId = 0,
                        IsPassed = false,
                        Reason = $"Глубина вложений: {method.ExecutableLines} (порог: 4). Метод: {method.ClassName}.{method.MethodName}",
                        CodeLocation = $"{method.FilePath}:{method.ClassName}.{method.MethodName}",
                        AiModel = "Roysln",
                        Confidence = null
                    });
                }

                if (verdicts.Any())
                {
                    _db.AuditVerdicts.AddRange(verdicts);
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

            _db.SessionAnalyses.Add(session);
            await _db.SaveChangesAsync();
            return session.SessionId;
        }

        //Обновляем статус сессии
        public async Task UpdateSessionAsync(int sessionId, CodeAnalysisResult result)
        {
            var session = await _db.SessionAnalyses.FindAsync(sessionId);
            if (session != null)
            {
                session.EndTime = DateTime.Now;
                session.Status = "Completed";
                await _db.SaveChangesAsync();
            }
        }
    }
}
