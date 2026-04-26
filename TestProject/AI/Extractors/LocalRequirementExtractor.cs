using UglyToad.PdfPig;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Windows.Shapes;
using System.IO;

namespace AI.Extractors
{
    public class LocalRequirementExtractor : IRequirementExtractor
    {
        public Task<ProjectSpecification> ExtractAsync(string pdfPath)
        {
            //Извлекаем текст из pdf
            string text = ExtractTextFromPdf(pdfPath);

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Не удалось извлечь текст из PDF. Файл может быть пустым или не содержит текста");
            }

            //Разбиваем текст на строки и убираем пустые значения
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            //Извлекаем требования
            var requirements = new List<Requirement>();
            int funcCount = 1, archCount = 1, metricCount = 1;

            foreach (var line in lines)
            {
                if (line.Length < 10) continue;

                if (TryExtractFunctional(line, out var funcReq))
                {
                    funcReq.RequirementType = "Functional";
                    funcReq.Title = $"FUNC-{funcCount:D2}: {funcReq.Title}";
                    requirements.Add(funcReq);
                    funcCount++;
                }
                else if (TryExtractArchitectural(line, out var archReq))
                {
                    archReq.RequirementType = "Architectural";
                    archReq.Title = $"FUNC-{archCount:D2}: {archReq.Title}";
                    requirements.Add(archReq);
                    archCount++;
                }
                else if (TryExtractMetric(line, out var metricReq))
                {
                    metricReq.RequirementType = "Metric";
                    metricReq.Title = $"FUNC-{metricCount:D2}: {metricReq.Title}";
                    requirements.Add(metricReq);
                    metricCount++;
                }
            }

            //При отстутствии требований, добавляем общие
            if (requirements.Count == 0)
            {
                foreach (var line in lines.Where(l => l.Length > 15).Take(20))
                {
                    requirements.Add(new Requirement
                    {
                        RequirementType = "Functional",
                        Category = "Общее",
                        Title = line.Length > 100 ? line[..100] : line,
                        Description = line,
                        Severity = "Major"
                    });
                }
            }
            //Формируем результат
            var specification = new ProjectSpecification
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(pdfPath),
                ExtractionType = "Local",
                Requirements = requirements,
                ExtractedAt = DateTime.UtcNow
            };
            return Task.FromResult(specification);
        }

        //метод, разделяющий строку на метрики
        private bool TryExtractMetric(string line, out Requirement requirement)
        {
            requirement = new Requirement();

            var patterns = new[]
            {
                @"качество\s+кода",
                @"стабильность\s+работы",
                @"корректное\s+поведение",
                @"удобство\s+работы",
                @"не\s+должна\s+превышать",
                @"не\s+более\s+\d+",
                @"цикломатическая\s+сложность",
                @"количество\s+строк"
            };
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
                    //извление числа (порога)
                    var threshold = ExtractThreshold(line);
                    requirement = new Requirement
                    {
                        Category = "Качество кода",
                        Title = line.Length > 120 ? line[..120] : line,
                        Description = line,
                        Severity = "Minor",
                        Threshold = threshold,
                        MetricName = DetermineMetricName(line)
                    };
                }
                return true;
            }
            return false;
        }

        //метод, определяющий имя метрики
        private string? DetermineMetricName(string line)
        {
            if (line.Contains("цикломатическая")) return "CyclomaticComplexity";
            if (line.Contains("строк")) return "LinesOfCode";
            if (line.Contains("вложенность")) return "NestingDepth";
            return null;
        }

        //метод, извлекающий числовой порог из строки
        private decimal? ExtractThreshold(string line)
        {
            var match = Regex.Match(line, @"\d+");
            if (match.Success && decimal.TryParse(match.Value, out var value))
                return value;
            return null;
        }

        //метод, разделяющий строку на архитектурные требования
        private bool TryExtractArchitectural(string line, out Requirement requirement)
        {
            requirement = new Requirement();

            var patterns = new[]
            {
                @"запрещено",
                @"не\s+должен.*содержать",
                @"не\s+должна.*зависеть",
                @"архитектурных\s+решений",
                @"правильность\s+архитектурных",
                @"разделение\s+на\s+слои",
                @"не\s+должен.*обращаться",
                @"слой\s+не\s+должен"
            };
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
                    requirement = new Requirement
                    {
                        Category = "Архитектурное ограничение",
                        Title = line.Length > 120 ? line[..120] : line,
                        Description = line,
                        Severity = "Major"
                    };
                }
                return true;
            }
            return false;
        }

        //метод, извлекающий весь текст из pdf
        private string ExtractTextFromPdf(string pdfPath)
        {
            using var document = PdfDocument.Open(pdfPath);

            var pages = document.GetPages();

            var pageTexts = pages.Select(page => page.Text);

            string result = string.Join("\n", pageTexts);

            return result;
        }

        //метод, разделяющий строку на фунциональные требования
        private bool TryExtractFunctional(string line, out  Requirement requirement)
        {
            requirement = new Requirement();

            var patterns = new[]
            {
                @"страница\s+должна",
                @"должна\s+отображать",
                @"должен\s+отображать",
                @"пользователь.*должен",
                @"система\s+должна",
                @"приложение\s+должно",
                @"реализовать\s+функционал",
                @"функционал\s+добавления",
                @"предоставляет\s+пользователю",
                @"поля:",
                @"поле\s+«",
                @"кнопка\s+редактирования",
                @"выбрать\s+одну\s+из",
                @"добавить\s+её",
                @"не\s+переходя\s+на\s+другую"
            };
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
                    requirement = new Requirement
                    {
                        Category = "Функциональное поведение",
                        Title = line.Length > 120 ? line[..120] : line,
                        Description = line,
                        Severity = DetermineSeverity(line),
                        AcceptanceCriteria = SerializeToList(line)
                    };
                }
                return true;
            }
            return false;
        }

        //метод, преобразующий в JSON-массив
        private string SerializeToList(string text)
        {
            return $@"[""{text.Replace("\"", "\\\"")}""]";
        }

        //метод, определяющий критичность требования
        private string DetermineSeverity(string line)
        {
            if (line.Contains("обязательно") || line.Contains("необходимо") || line.Contains("должна"))
                return "Fatal";
            if (line.Contains("желательно") || line.Contains("плюсом") || line.Contains("по желанию"))
                return "Minor";
            return "Major";
        }
    }
}
