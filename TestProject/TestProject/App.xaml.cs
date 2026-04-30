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

            //Загрузка словаря
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
            /*
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║     ТЕСТ ROSLYN АНАЛИЗАТОРА         ║");
            Console.WriteLine("╚══════════════════════════════════════╝\n");

            if (!Directory.Exists(projectPath))
            {
                Console.WriteLine($"Папка не найдена: {projectPath}");
                Console.ReadKey();
                Shutdown();
                return;
            }

            var loader = new DictionaryLoader(db);
            string dictPath = @"..\..\..\..\russian-words.txt";
            int count = await loader.LoadFromFileAsync(dictPath, minLength: 3);
            Console.WriteLine($"Загружено слов: {count}");

            var analyzer = new RoslynSyntaxAnalyzer();

            Console.WriteLine("Анализируем проект...\n");
            var result = await analyzer.AnalyzeProjectAsync(projectPath);

            Console.WriteLine($"Проанализировано методов: {result.TotalMethods}\n");

            // Сводка
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("           СВОДКА МЕТРИК              ");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"  Всего методов:               {result.TotalMethods,5}");
            Console.WriteLine($"  Средняя цикломатическая:     {result.AvgCyclomaticComplexity,5:F1}  (норма < 10)");
            Console.WriteLine($"  Средняя когнитивная:         {result.AvgCognitiveComplexity,5:F1}  (норма < 15)");
            Console.WriteLine($"  Среднее строк (ELOC):        {result.AvgExecutableLines,5:F1}  (норма < 20)");
            Console.WriteLine($"  Методов > 10 (CC):           {result.MethodsExceedingComplexity,5} !");
            Console.WriteLine($"  Методов > 20 (ELOC):         {result.MethodsExceedingLines,5}  !");
            Console.WriteLine("═══════════════════════════════════════\n");

            if (result.Methods.Count > 0)
            {
                // Топ-5 по цикломатической сложности
                Console.WriteLine("=== ТОП-5 ПО ЦИКЛОМАТИЧЕСКОЙ СЛОЖНОСТИ ===\n");
                Console.WriteLine($"{"CC",4} {"Когн",4} {"ELOC",4} {"Влож",4} {"Комм%",5}  Метод");
                Console.WriteLine(new string('─', 75));

                foreach (var m in result.Methods
                    .OrderByDescending(m => m.CyclomaticComplexity)
                    .Take(5))
                {
                    Console.WriteLine($"{m.CyclomaticComplexity,3}  " +
                        $"{m.CognitiveComplexity,3}  " +
                        $"{m.ExecutableLines,3}  " +
                        $"{m.NestingDepth,3}  " +
                        $"{m.CommentDensity,4:F0}  " +
                        $"  {m.ClassName}.{m.MethodName}()");
                }

                // Топ-5 по когнитивной сложности
                Console.WriteLine($"\n=== ТОП-5 ПО КОГНИТИВНОЙ СЛОЖНОСТИ ===\n");
                Console.WriteLine($"{"CC",4} {"Когн",4} {"ELOC",4} {"Влож",4} {"Комм%",5}  Метод");
                Console.WriteLine(new string('─', 75));

                foreach (var m in result.Methods
                    .OrderByDescending(m => m.CognitiveComplexity)
                    .Take(5))
                {
                    Console.WriteLine($"{m.CyclomaticComplexity,3}  " +
                        $"{m.CognitiveComplexity,3}  " +
                        $"{m.ExecutableLines,3}  " +
                        $"{m.NestingDepth,3}  " +
                        $"{m.CommentDensity,4:F0}  " +
                        $"  {m.ClassName}.{m.MethodName}()");
                }

                // Распределение цикломатической сложности
                Console.WriteLine($"\n=== РАСПРЕДЕЛЕНИЕ ЦИКЛОМАТИЧЕСКОЙ СЛОЖНОСТИ ===");
                Console.WriteLine($"{" 1-5  (отлично):  "}{Bar(result.Methods.Count(m => m.CyclomaticComplexity <= 5), result.TotalMethods)}");
                Console.WriteLine($"{" 6-10 (норма):    "}{Bar(result.Methods.Count(m => m.CyclomaticComplexity > 5 && m.CyclomaticComplexity <= 10), result.TotalMethods)}");
                Console.WriteLine($"{"11-20 (сложно):  "}{Bar(result.Methods.Count(m => m.CyclomaticComplexity > 10 && m.CyclomaticComplexity <= 20), result.TotalMethods)}");
                Console.WriteLine($"{"21+  (критично): "}{Bar(result.Methods.Count(m => m.CyclomaticComplexity > 20), result.TotalMethods)}");

                // Распределение когнитивной сложности
                Console.WriteLine($"\n=== РАСПРЕДЕЛЕНИЕ КОГНИТИВНОЙ СЛОЖНОСТИ ===");
                Console.WriteLine($"{" 1-10  (отлично): "}{Bar(result.Methods.Count(m => m.CognitiveComplexity <= 10), result.TotalMethods)}");
                Console.WriteLine($"{"11-15 (норма):   "}{Bar(result.Methods.Count(m => m.CognitiveComplexity > 10 && m.CognitiveComplexity <= 15), result.TotalMethods)}");
                Console.WriteLine($"{"16-25 (сложно): "}{Bar(result.Methods.Count(m => m.CognitiveComplexity > 15 && m.CognitiveComplexity <= 25), result.TotalMethods)}");
                Console.WriteLine($"{"26+  (критично):"}{Bar(result.Methods.Count(m => m.CognitiveComplexity > 25), result.TotalMethods)}");

                // Список всех методов
                Console.WriteLine($"\n=== ВСЕ МЕТОДЫ ({result.TotalMethods} шт.) ===\n");
                Console.WriteLine($"{"CC",4} {"Когн",4} {"ELOC",4} {"Влож",4} {"Комм%",5}  Метод");
                Console.WriteLine(new string('─', 75));

                foreach (var m in result.Methods.OrderBy(m => m.ClassName).ThenBy(m => m.MethodName))
                {
                    string flags = "";
                    if (m.CyclomaticComplexity > 10) flags += "CC ";
                    if (m.CognitiveComplexity > 15) flags += "Когн ";
                    if (m.ExecutableLines > 20) flags += "ELOC ";

                    Console.WriteLine($"{m.CyclomaticComplexity,3}  " +
                        $"{m.CognitiveComplexity,3}  " +
                        $"{m.ExecutableLines,3}  " +
                        $"{m.NestingDepth,3}  " +
                        $"{m.CommentDensity,4:F0}  " +
                        $"  {m.ClassName}.{m.MethodName}()" +
                        (flags.Length > 0 ? $"  [{flags.Trim()}]" : ""));
                }
            }

            // Проверка на пустой проект
            Console.WriteLine("=== ПРОВЕРКА НА ПУСТОЙ ПРОЕКТ ===\n");
            var validator = new ProjectValidator();
            var emptyResult = validator.CheckIfEmpty(projectPath);

            Console.WriteLine($"Пустой проект: {(emptyResult.IsEmpty ? "ДА" : "НЕТ")}");
            Console.WriteLine($"Уровень срабатывания: {emptyResult.FailLevel}");
            Console.WriteLine($"Причина: {emptyResult.Reason}");
            Console.WriteLine($"Файлов: {emptyResult.TotalFiles}");
            Console.WriteLine($"Классов: {emptyResult.TotalClasses} (пользовательских: {emptyResult.UserClasses})");
            Console.WriteLine($"Методов: {emptyResult.TotalMethods} (значимых: {emptyResult.MeaningfulMethods})");
            Console.WriteLine($"Совпадений с шаблонами: {emptyResult.TemplateMatches}");
            Console.WriteLine($"Уверенность: {emptyResult.Confidence:P0}\n");

            // Проверка компиляции
            Console.WriteLine("=== ПРОВЕРКА КОМПИЛЯЦИИ ===\n");
            var compResult = validator.CheckCompilation(projectPath);

            Console.WriteLine($"Компилируется: {(compResult.CanCompile ? "ДА" : "НЕТ")}");
            Console.WriteLine($"Ошибок: {compResult.ErrorCount}, Предупреждений: {compResult.WarningCount}");

            if (compResult.Errors.Count > 0)
            {
                Console.WriteLine($"\nПервые 10 ошибок:");
                foreach (var err in compResult.Errors.Take(10))
                {
                    Console.WriteLine($" {err}");
                }
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
            Shutdown();
        }

        private static string Bar(int count, int total)
        {
            if (total == 0) return "";
            int width = (int)((double)count / total * 20);
            return new string('█', width) + $" {count}";
        //}
        */

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
        }
    }
}
