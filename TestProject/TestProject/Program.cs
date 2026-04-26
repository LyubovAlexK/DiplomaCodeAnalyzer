using AI.Extractors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestProject
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== ТЕСТ МОДУЛЯ 3: ЛОКАЛЬНЫЙ АНАЛИЗАТОР ТЗ ===\n");

            // Путь к тестовому PDF (в папке с решением)
            string pdfPath = @"..\..\..\..\csharp.pdf";

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"❌ Файл не найден: {pdfPath}");
                Console.WriteLine("Скопируйте csharp.pdf в папку с решением (.sln) и запустите снова.");
                return;
            }

            Console.WriteLine($"📄 Файл найден: {pdfPath}\n");

            // Создаём анализатор через фабрику (isOnline = false — локальный режим)
            var factory = new ExtractorFactory();
            var extractor = factory.Create(isOnline: false);

            try
            {
                Console.WriteLine("⏳ Анализируем ТЗ...\n");
                var specification = await extractor.ExtractAsync(pdfPath);

                // Выводим результат
                Console.WriteLine($"✅ Анализ завершён!");
                Console.WriteLine($"   Название ТЗ: {specification.Title}");
                Console.WriteLine($"   Тип анализа: {specification.ExtractionType}");
                Console.WriteLine($"   Дата анализа: {specification.ExtractedAt}");
                Console.WriteLine($"   Всего требований: {specification.Requirements.Count}\n");

                // Группируем по типам
                var functional = specification.Requirements.Where(r => r.RequirementType == "Functional").ToList();
                var architectural = specification.Requirements.Where(r => r.RequirementType == "Architectural").ToList();
                var metric = specification.Requirements.Where(r => r.RequirementType == "Metric").ToList();

                Console.WriteLine($"   ┌─ Functional:    {functional.Count} шт.");
                Console.WriteLine($"   ├─ Architectural: {architectural.Count} шт.");
                Console.WriteLine($"   └─ Metric:        {metric.Count} шт.\n");

                // Выводим все требования подробно
                Console.WriteLine("=== ДЕТАЛЬНЫЙ СПИСОК ТРЕБОВАНИЙ ===\n");

                foreach (var req in specification.Requirements)
                {
                    Console.WriteLine($"━━━ [{req.RequirementType}] {req.Title}");
                    Console.WriteLine($"   Категория: {req.Category ?? "—"}");
                    Console.WriteLine($"   Критичность: {req.Severity}");
                    Console.WriteLine($"   Описание: {Truncate(req.Description, 150)}");

                    if (req.Threshold.HasValue)
                        Console.WriteLine($"   Порог: {req.Threshold}");

                    if (!string.IsNullOrWhiteSpace(req.MetricName))
                        Console.WriteLine($"   Название метрики: {req.MetricName}");

                    Console.WriteLine();
                }

                // Красивый JSON-вывод
                Console.WriteLine("=== JSON-ПРЕДСТАВЛЕНИЕ ===\n");
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                Console.WriteLine(JsonSerializer.Serialize(specification, jsonOptions));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при анализе: {ex.Message}");
                Console.WriteLine($"   Тип ошибки: {ex.GetType().Name}");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        static string Truncate(string? text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text)) return "—";
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }
    }
}
