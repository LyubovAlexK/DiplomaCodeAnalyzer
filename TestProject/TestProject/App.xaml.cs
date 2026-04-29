using AI.Extractors;
using AI.Services;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using UglyToad.PdfPig;

namespace TestProject
{
    public partial class App : Application
    {

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //Тест для локалки
            /* 
            string pdfPath = @"..\..\..\..\TechBuild\testTask.pdf";

            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("Файл не найден");
                Shutdown();
                return;
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using var db = new AppDbContext(options);
            var dictService = new DictionaryService(db);
            var factory = new ExtractorFactory(dict: dictService);
            var extractor = factory.Create(isOnline: false);

            try
            {
                // 10 и AI, и локалка
                var specification = await extractor.ExtractAsync(pdfPath, specificationId: 10);

                Console.WriteLine($"ЛОКАЛЬНЫЙ АНАЛИЗ: {specification.Requirements.Count} требований\n");

                foreach (var req in specification.Requirements)
                {
                    Console.WriteLine($"[{req.RequirementType}] {req.Severity} | {req.Title}");
                    if (req.MetricName != null)
                        Console.WriteLine($"   Metric: {req.MetricName} (порог: {req.Threshold?.ToString() ?? "нет"})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
            Shutdown();
            */

            //Тест для AI

            string filePath = @"..\..\..\..\TechBuild\csharp.odt";

            if (!File.Exists(filePath))
            {
                Console.WriteLine("Файл не найден: " + filePath);
                Shutdown();
                return;
            }

            string apiKey = "MDE5ZGQ0NmEtYzcxYi03ZDY2LThhYTAtNDZmOTZhMTY5ZGFiOjc5ZmFlMzU0LWY1YmItNGY1My1hMGZlLTAyOWJmZThlMjAwYg==";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using var db = new AppDbContext(options);
            var dictService = new DictionaryService(db);
            var factory = new ExtractorFactory(dict: dictService, apiKey: apiKey);
            var extractor = factory.Create(isOnline: true);

            try
            {
                Console.WriteLine("=== GIGACHAT AI АНАЛИЗ ===\n");
                Console.WriteLine($"Файл: {filePath}\n");

                var specification = await extractor.ExtractAsync(filePath);

                Console.WriteLine($"\n=== РЕЗУЛЬТАТ: {specification.Requirements.Count} требований ===\n");

                foreach (var req in specification.Requirements)
                {
                    Console.WriteLine($"[{req.RequirementType}] {req.Severity} | {req.Title}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nОшибка: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
            Shutdown();
        }
    }
}
