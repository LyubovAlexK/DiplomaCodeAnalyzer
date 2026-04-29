using Core.Models;
using Core.Services;
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
        private readonly DictionaryService _dict;

        public LocalRequirementExtractor(DictionaryService dict)
        {
            _dict = dict;
        }
        public async Task<ProjectSpecification> ExtractAsync(string filePath, int specificationId)
        {
            // 1. Извлекаем текст через конвертер
            string text = DocumentConverter.ExtractText(filePath);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Не удалось извлечь текст из документа.");

            // 2. Разбиваем слипшийся текст
            var separators = await _dict.GetSeparatorsAsync();
            text = SplitWordsDynamic(text, separators);

            // 3. Загружаем маркеры требований из БД
            var allMarkers = await _dict.GetMarkersAsync(specificationId);


            int testedSentences = 0;
            int matchedSentences = 0;

            // 4. Разбиваем на предложения
            var sentences = text.Split(new[] { ". ", ".\n", ".\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 15)
                .ToList();


            // ДИАГНОСТИКА
            Console.WriteLine($"Всего предложений: {sentences.Count}");
            Console.WriteLine($"Всего маркеров: {allMarkers.Count}");

            // 5. Извлекаем требования, используя маркеры из БД
            var requirements = new List<Requirement>();
            var counters = new Dictionary<string, int> { { "Functional", 1 }, { "Architectural", 1 }, { "Metric", 1 } };
            
            foreach (var sentence in sentences)
            {
                string fullSentence = sentence + ".";
                testedSentences++;
                var matchedMarker = FindBestMatch(fullSentence, allMarkers);

                if (matchedMarker != null)
                {
                    matchedSentences++;
                    string type = matchedMarker.RequirementType;
                    string prefix = type switch
                    {
                        "Functional" => "FUNC",
                        "Architectural" => "ARCH",
                        "Metric" => "QUAL",
                        _ => "GEN"
                    };

                    requirements.Add(new Requirement
                    {
                        RequirementType = type,
                        Category = type switch
                        {
                            "Functional" => "Функциональное поведение",
                            "Architectural" => "Архитектурное ограничение",
                            "Metric" => "Качество кода",
                            _ => "Общее"
                        },
                        Title = $"{prefix}-{counters[type]++:D2}: {fullSentence[..Math.Min(fullSentence.Length, 100)]}",
                        Description = fullSentence,
                        Severity = matchedMarker.DefaultSeverity,
                        MetricName = type == "Metric" ? matchedMarker.Phrase : null,
                        Threshold = ExtractThreshold(fullSentence),
                        AcceptanceCriteria = $@"[""{fullSentence.Replace("\"", "\\\"")}""]"
                    });
                }
            }
            Console.WriteLine($"Проверено предложений: {testedSentences}");
            Console.WriteLine($"Найдено совпадений: {matchedSentences}");

            return new ProjectSpecification
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(filePath),
                ExtractionType = "Local",
                Requirements = requirements
            };
        }

        private RequirementMarker? FindBestMatch(string line, List<RequirementMarker> markers)
        {
            string compact = line.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

            foreach (var marker in markers.OrderByDescending(m => m.Phrase.Length))
            {
                string compactMarker = marker.Phrase.Replace(" ", "");
                if (Regex.IsMatch(compact, Regex.Escape(compactMarker), RegexOptions.IgnoreCase))
                {
                    marker.RequirementType ??= "Functional";
                    marker.DefaultSeverity ??= "Major";
                    return marker;
                }
            }
            return null;
        }

        //Порог для метрик

        private decimal? ExtractThreshold(string line)
        {
            var match = Regex.Match(line, @"\d+");
            if (match.Success && decimal.TryParse(match.Value, out var value))
                return value;
            return null;
        }

        //Разбивка текста

        private string SplitWordsDynamic(string text, List<string> words)
        {
            foreach (var word in words.OrderByDescending(w => w.Length))
            {
                text = Regex.Replace(text,
                    $"(?<=[а-яёa-z)»,.]){Regex.Escape(word)}(?=[а-яёa-z(«,.])",
                    $" {word} ",
                    RegexOptions.IgnoreCase);
            }
            return Regex.Replace(text, @"\s+", " ").Trim();
        }
    }
}
