using Catalyst;
using Mosaik.Core;
using Core.Models;
using Core.Services;
using System.Text.RegularExpressions;

namespace AI.Extractors
{
    public class LocalRequirementExtractor : IRequirementExtractor
    {
        private readonly DictionaryService _dict;

        public LocalRequirementExtractor(DictionaryService dict)
        {
            _dict = dict;
        }

        private static Pipeline? _pipeline;
        private static readonly object _lock = new();
        private static bool _catalystInitialized = false;

        public async Task<ProjectSpecification> ExtractAsync(string filePath, int specificationId)
        {
            // 1. Извлекаем текст через конвертер
            string text = DocumentConverter.ExtractText(filePath);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Не удалось извлечь текст из документа.");

            // 2. Разбиваем слипшийся текст через Catalyst
            try
            {
                text = SegmentWithCatalyst(text);
                Console.WriteLine("Catalyst: текст восстановлен");  
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Catalyst недоступен: {ex.Message}");
                // Если Catalyst не сработал — оставляем текст как есть
            }

            // 3. Разбиваем на предложения (простая эвристика)
            var sentences = text.Split(new[] { ". ", ".\n", ".\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 15)
                .ToList();

            // 4. Загружаем маркеры требований из БД
            var allMarkers = await _dict.GetMarkersAsync(specificationId);

            // ДИАГНОСТИКА
            Console.WriteLine($"Всего предложений: {sentences.Count}");
            Console.WriteLine($"Всего маркеров: {allMarkers.Count}");

            // 5. Извлекаем требования, используя маркеры из БД
            var requirements = new List<Requirement>();
            var counters = new Dictionary<string, int> { { "Functional", 1 }, { "Architectural", 1 }, { "Metric", 1 } };
            int testedSentences = 0, matchedSentences = 0;

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

        private decimal? ExtractThreshold(string line)
        {
            var match = Regex.Match(line, @"\d+");
            if (match.Success && decimal.TryParse(match.Value, out var value))
                return value;
            return null;
        }

        private string SegmentWithCatalyst(string text)
        {
            if (!_catalystInitialized)
            {
                lock (_lock)
                {
                    if (!_catalystInitialized)
                    {
                        Catalyst.Models.Russian.Register();
                        Storage.Current = new DiskStorage("catalyst-models");
                        _pipeline = Pipeline.ForAsync(Mosaik.Core.Language.Russian).GetAwaiter().GetResult();
                        _catalystInitialized = true;
                    }
                }
            }

            var doc = new Document(text, Mosaik.Core.Language.Russian);
            _pipeline!.ProcessSingle(doc);

            var words = new List<string>();
            foreach (var span in doc.Spans)
            {
                foreach (var token in span.Tokens)
                {
                    words.Add(token.Value);
                }
            }

            return string.Join(" ", words);
        }
    }
}