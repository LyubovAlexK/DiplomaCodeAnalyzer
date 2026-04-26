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
            var sentences = text.Split(new[] { ". ", ".\n", ".\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 15)
                .ToList();

            //Извлекаем требования
            var requirements = new List<Requirement>();
            int funcCount = 1, archCount = 1, metricCount = 1;

            if (text.Contains("Критерии оценки"))
            {
                var criteriaBlock = text.Split("Критерии оценки")[1];
                var criteriaLines = criteriaBlock.Split(new[] { "\n", "•", "", "●", "○" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 5);

                foreach (var line in criteriaLines)
                {
                    requirements.Add(new Requirement
                    {
                        RequirementType = "Metric",
                        Category = "Критерии оценки",
                        Title = $"METR-{metricCount:D2}: {line[..Math.Min(line.Length, 100)]}",
                        Description = line,
                        Severity = "Major"
                    });
                    metricCount++;
                }
            }

            foreach (var sentence in sentences)
            {
                string fullSentence = sentence + ".";

                if (fullSentence.Contains("Критерии оценки"))
                    continue;

                if (TryExtractFunctional(fullSentence, out var funcReq))
                {
                    funcReq.RequirementType = "Functional";
                    funcReq.Title = $"FUNC-{funcCount:D2}: {funcReq.Title}";
                    requirements.Add(funcReq);
                    funcCount++;
                }
                else if (TryExtractArchitectural(fullSentence, out var archReq))
                {
                    archReq.RequirementType = "Architectural";
                    archReq.Title = $"ARCH-{archCount:D2}: {archReq.Title}";
                    requirements.Add(archReq);
                    archCount++;
                }
            }
                //Формируем результат
                var specification = new ProjectSpecification
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(pdfPath),
                    ExtractionType = "Local",
                    Requirements = requirements
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
                @"правильность\s+архитектурных",
                @"стабильность\s+работы",
                @"корректное\s+поведение",
                @"удобство\s+работы",
                @"полнота\s+выполнения"
            };
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
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
                        Title = line.Length > 100 ? line[..100] : line,
                        Description = line,
                        Severity = "Major"
                    };
                    return true;
                }
            }
            requirement = new Requirement();
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
            var patterns = new[]
            {
                @"страница.*предоставляет",
                @"страница.*должна",
                @"должна\s+отображать",
                @"должен\s+быть",
                @"должна\s+присутствовать",
                @"позволяет\s+выбрать",
                @"добавить\s+её",
                @"содержит\s+следующие\s+поля",
                @"приложение\s+должно",
                @"реализовать\s+функционал",
                @"список\s+стран\s+фиксированный",
                @"кнопка\s+редактирования",
                @"изменить\s+отображения",
                @"решение\s+должно\s+быть\s+предоставлено",
                @"язык\s+разработки",
                @"можно\s+использовать",
                @"для\s+бекэнда",
                @"состоять\s+из\s+двух\s+страниц",
                @"переключатель\s+между\s+ними",
                @"имя.*фамилия.*пол.*дата",
                @"название\s+команды",
                @"выбрать\s+одну\s+из",
                @"SignalR"
            };
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                {
                    requirement = new Requirement
                    {
                        Category = "Функциональное поведение",
                        Title = line.Length > 100 ? line[..100] : line,
                        Description = line,
                        Severity = DetermineSeverity(line),
                        AcceptanceCriteria = SerializeToList(line)
                    };
                    return true;
                }
            }
            requirement = new Requirement();
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
