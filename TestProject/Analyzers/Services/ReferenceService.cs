using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace Analyzers.Services
{
    public class ReferenceService
    {
        private readonly AppDbContext _db;

        public ReferenceService(AppDbContext db)
        {
            _db = db;
        }

        //Анализ эталонного проекта и сохранение метрик

        public async Task<int> SaveReferenceAsync(int specificationId, string projectPath, CodeAnalysisResult result)
        {
            var reference = new ReferenceProject
            {
                SpecificationId = specificationId,
                ProjectPath = projectPath,
                TotalClasses = 0, //заполняется из ProjectValidator
                TotalMethods = result.TotalMethods,
                AvgCyclomaticComplexity = (decimal)result.AvgCyclomaticComplexity,
                AvgCognitiveComplexity = (decimal)result.AvgCognitiveComplexity,
                AvgExecutableLines = (decimal)result.AvgExecutableLines,
                MethodsExceedingComplexity = result.MethodsExceedingComplexity,
                MethodsExceedingLines = result.MethodsExceedingLines
            };

            _db.ReferenceProjects.Add(reference);
            await _db.SaveChangesAsync();

            //Создаем правила по умолчанию с допуском +-20 от эталона
            var defaultRules = new[]
            {
                new ReferenceRule {
                    ReferenceId = reference.ReferenceId,
                    MetricName = "TotalMethods",
                    ThresholdType = "Range",
                    ThresholdValue = (int)(result.TotalMethods * 0.2m)
                },
                new ReferenceRule
                {
                    ReferenceId = reference.ReferenceId,
                    MetricName = "AvgCyclomaticComplexity",
                    ThresholdType = "Max",
                    ThresholdValue = 1.5m * (decimal)result.AvgCyclomaticComplexity
                },
                new ReferenceRule
                {
                    ReferenceId = reference.ReferenceId,
                    MetricName = "AvgExecutableLines",
                    ThresholdType = "Max",
                    ThresholdValue = 1.5m * (decimal)result.AvgExecutableLines
                },
                new ReferenceRule
                {
                    ReferenceId = reference.ReferenceId,
                    MetricName = "MethodsExceedingComplexity",
                    ThresholdType = "Range",
                    ThresholdValue = result.MethodsExceedingComplexity + 2
                }
            };

            _db.ReferenceRules.AddRange(defaultRules);
            await _db.SaveChangesAsync();

            return reference.ReferenceId;
        }

        //Сравнение кода стажёра с эталоном
        public async Task<List<string>> CompareWithReference(int referenceId, CodeAnalysisResult traineeResult)
        {
            var reference = await _db.ReferenceProjects.FindAsync(referenceId);
            var rules = await _db.ReferenceRules.Where(r => r.ReferenceId == referenceId && r.IsEnabled).ToListAsync();
            var violations = new List<string>();

            if (reference == null) return violations;

            foreach (var rule in rules)
            {
                switch (rule.MetricName)
                {
                    case "TotalMethods":
                        var expectedMethods = reference.TotalMethods ?? 0;
                        var range = rule.ThresholdValue ?? (int)(expectedMethods * 0.2m);
                        if (Math.Abs(traineeResult.TotalMethods - expectedMethods) > range)
                        {
                            violations.Add($"Методов: {traineeResult.TotalMethods} (эталон: {expectedMethods}, допуск: +- {range})");
                        }
                        break;
                    case "AvgCyclomaticComplexity":
                        var maxComplexity = rule.ThresholdValue ?? 1.5m * reference.AvgCyclomaticComplexity ?? 15;
                        if ((decimal)traineeResult.AvgCyclomaticComplexity > maxComplexity)
                        {
                            violations.Add($"Средняя сложность: {traineeResult.AvgCyclomaticComplexity:F1} (порог: {maxComplexity:F1})");
                        }
                        break;
                    case "AvgExecutableLines":
                        var maxLines = rule.ThresholdValue ?? 1.5m * reference.AvgExecutableLines ?? 30;
                        if ((decimal)traineeResult.AvgExecutableLines > maxLines)
                        {
                            violations.Add($"Среднее строк: {traineeResult.AvgExecutableLines:F1} (порог: {maxLines:F1})");
                        }
                        break;
                    case "MethodsExceedingComplexity":
                        var maxExceeding = rule.ThresholdValue ?? 2;
                        if ((decimal)traineeResult.MethodsExceedingComplexity > maxExceeding)
                        {
                            violations.Add($"Методов с превышением сложности: {traineeResult.MethodsExceedingComplexity} (порог: {maxExceeding})");
                        }
                        break;
                }
                Console.WriteLine($"Эталон: сложных методов = {reference.MethodsExceedingComplexity}, порог = {rule.ThresholdValue}");
                Console.WriteLine($"Стажёр: сложных методов = {traineeResult.MethodsExceedingComplexity}");
            }
            return violations;
        }
    }
}
