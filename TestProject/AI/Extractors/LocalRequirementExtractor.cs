using Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using UglyToad.PdfPig;
using static System.Net.Mime.MediaTypeNames;

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
                    var normalizedLine = NormalizeText(line);
                    requirements.Add(new Requirement
                    {
                        RequirementType = "Metric",
                        Category = "Критерии оценки",
                        Title = $"METR-{metricCount:D2}: {normalizedLine[..Math.Min(normalizedLine.Length, 100)]}",
                        Description = normalizedLine,
                        Severity = "Major"
                    });
                    metricCount++;
                }
            }

            foreach (var sentence in sentences)
            {
                string fullSentence = NormalizeText(sentence + ".");

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
            string compact = line.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

            var patterns = new[]
            {
                @"запрещено",
                @"недолженсодержать",
                @"недолжназависеть",
                @"архитектурныхрешений",
                @"правильностьархитектурных",
                @"разделениенаслои",
                @"недолженобращаться",
                @"слойнедолжен"
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
        private string ExtractTextFromPdf(string path)
        {
            using var document = PdfDocument.Open(path);
            var rawText = string.Join("\n", document.GetPages().Select(p => p.Text));
            return NormalizeText(rawText);
        }

        private string NormalizeText(string text)
        {

            if (string.IsNullOrWhiteSpace(text))
                return text;

            // 1. Пробел после строчной перед заглавной: "системы<SoccerStat>" → "системы <SoccerStat>"
            text = Regex.Replace(text, @"([а-яёa-z])([A-ZА-ЯЁ<])", "$1 $2");

            // 2. Пробел после точки/двоеточия/вопроса + заглавная или строчная
            text = Regex.Replace(text, @"([.:?!;])([А-ЯЁA-Zа-яёa-z])", "$1 $2");

            // 3. Пробел после цифры перед буквой: "2025г." → "2025 г."
            text = Regex.Replace(text, @"(\d)([А-ЯЁA-Zа-яёa-z])", "$1 $2");

            // 4. Пробел после буквы перед цифрой: "из20" → "из 20"
            text = Regex.Replace(text, @"([а-яёa-z])(\d)", "$1 $2", RegexOptions.IgnoreCase);

            // 5. Пробел после запятой
            text = Regex.Replace(text, @",(\S)", ", $1");

            // 6. Пробел после скобок
            text = Regex.Replace(text, @"\)(\S)", ") $1");
            text = Regex.Replace(text, @"(\S)\(", "$1 (");

            // 7. Пробел после точки с запятой
            text = Regex.Replace(text, @";(\S)", "; $1");

            // 8. Убираем лишние пробелы
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }

        //метод, разделяющий строку на фунциональные требования
        private bool TryExtractFunctional(string line, out  Requirement requirement)
        {
            string compact = line.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

            var patterns = new[]
            {
                // Без пробелов!
                @"системадолжнаобеспечивать",
                @"просмотрсписка",
                @"системапредназначена",
                @"основныефункции",
                @"требованиякклиентской",
                @"требованиякграфическому",
                @"требованиякинтеграции",
                @"требованиякгеографии",
                @"требованиякпользователям",
                @"описаниеинтерфейса",
                @"должныкорректноотображаться",
                @"поддержкапортретной",
                @"гарантированаработа",
                @"учетвременныхзон",
                @"русскийинтерфейс",
                @"фильтрподате",
                @"текстовыйпоиск",
                @"навигационнаяцепочка",
                @"пагинация",
                @"требованияксдаче",
                @"ключикапи",
                // Старые паттерны тоже переводим в безпробельный формат
                @"страницапредоставляет",
                @"страницадолжна",
                @"должнаотображать",
                @"долженбыть",
                @"должнаприсутствовать",
                @"позволяетвыбрать",
                @"содержитследующиевполя",
                @"приложениедолжно",
                @"реализоватьфункционал",
                @"кнопкаредактирования",
                @"решениедолжнобытьпредоставлено",
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
