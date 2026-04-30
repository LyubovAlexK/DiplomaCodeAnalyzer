using AI.Extractors;
using AI.Services;
using Analyzers;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
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
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        private string projectPath = @"D:\DITI\CsharpProjectTest\BlanckTest";
        protected override async void OnStartup(StartupEventArgs e)
        {
            AllocConsole(); //Для анализатора
            base.OnStartup(e);

            //Тестирование локалки ТЗ
            /*
            string pdfPath = @"..\..\..\..\TechBuild\testTask.docx";

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
                var specification = await extractor.ExtractAsync(pdfPath, specificationId: 4);

                Console.WriteLine($"Требований: {specification.Requirements.Count}\n");

                foreach (var req in specification.Requirements)
                {
                    Console.WriteLine($"[{req.RequirementType}] {req.Severity} | {req.Title}");
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


            //Тестирование анализатора

            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"Папка не найдена: {projectPath}");
                Console.WriteLine("Создайте тестовый C# проект или укажите другой путь.");
                Console.ReadKey();
                Shutdown();
                return;
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using var db = new AppDbContext(options);

            // 1. Проверка проекта на пустоту
            var validator = new ProjectValidator();
            var emptyCheck = validator.CheckIfEmpty(projectPath);

            Console.WriteLine("=== ПРОВЕРКА ПРОЕКТА ===");
            Console.WriteLine($"Проект пустой: {emptyCheck.IsEmpty}");
            Console.WriteLine($"Причина: {emptyCheck.Reason}");
            Console.WriteLine($"Уверенность: {emptyCheck.Confidence}");
            Console.WriteLine($"Классов: {emptyCheck.TotalClasses}");
            Console.WriteLine($"Методов: {emptyCheck.TotalMethods}");
            Console.WriteLine($"Осмысленных методов: {emptyCheck.MeaningfulMethods}");
            Console.WriteLine();

            if (emptyCheck.IsEmpty)
            {
                Console.WriteLine("Проект пустой — анализ не требуется.");
                Console.ReadKey();
                Shutdown();
                return;
            }

            // 2. Анализ Roslyn
            var roslynAnalyzer = new RoslynSyntaxAnalyzer();
            var result = await roslynAnalyzer.AnalyzeProjectAsync(projectPath);

            Console.WriteLine("=== ROSLYN АНАЛИЗ ===");
            Console.WriteLine($"Всего методов: {result.TotalMethods}");
            Console.WriteLine($"Средняя цикломатическая сложность: {result.AvgCyclomaticComplexity:F2}");
            Console.WriteLine($"Средняя когнитивная сложность: {result.AvgCognitiveComplexity:F2}");
            Console.WriteLine($"Среднее исполняемых строк: {result.AvgExecutableLines:F2}");
            Console.WriteLine($"Методов с превышением сложности (>10): {result.MethodsExceedingComplexity}");
            Console.WriteLine($"Методов с превышением строк (>20): {result.MethodsExceedingLines}");
            Console.WriteLine();

            // 3. Сохраняем в БД
            var roslynService = new RoslynResultService(db);

            // Создаём сессию (заглушка: TraineeId=1, ProjectId=1, SpecificationId=4)
            int sessionId = await roslynService.CreateSessionAsync(
                traineeId: 1,
                projectId: 1,
                specificationId: 4,
                isAiAvailable: false);

            // Сохраняем нарушения
            await roslynService.SaveResultsAsync(sessionId, result);

            // Обновляем сессию
            await roslynService.UpdateSessionAsync(sessionId, result);

            Console.WriteLine($"=== СОХРАНЕНО В БД ===");
            Console.WriteLine($"SessionId: {sessionId}");

            // Проверяем, что сохранилось
            var savedVerdicts = await db.AuditVerdicts
                .Where(v => v.SessionId == sessionId)
                .ToListAsync();

            Console.WriteLine($"Вердиктов сохранено: {savedVerdicts.Count}");
            foreach (var v in savedVerdicts)
            {
                Console.WriteLine($"  [{v.AiModel}] {(v.IsPassed ? "OK" : "НАРУШЕНИЕ")} | {v.Reason?[..Math.Min(80, v.Reason.Length)]}");
            }

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
            Shutdown();

            //Тест для AI
            /*
            string filePath = @"..\..\..\..\TechBuild\csharp.docx";

            if (!File.Exists(filePath))
            {
                Console.WriteLine("Файл не найден: " + filePath);
                Shutdown();
                return;
            }

            string apiKey = "MDE5ZGQ0NmEtYzcxYi03ZDY2LThhYTAtNDZmOTZhMTY5ZGFiOjFhN2UxZGMyLTY0ODktNDE4Ni1hZmI4LTA1NzExMDQ0OTk4OA==";

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
                    Console.WriteLine($"[{req.Description}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nОшибка: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу...");
            Console.ReadKey();
            Shutdown();
            */
        }
    }
}
