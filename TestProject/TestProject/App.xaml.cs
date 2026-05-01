using AI.Extractors;
using AI.Services;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace TestProject;

public partial class App : Application
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    // Пути
    private string tzPath = @"..\..\..\..\TechBuild\moviedatabase.docx";
    private string projectPath = @"D:\DITI\CsharpProjectTest\MovieDatabase\IFN664 Assignment";
    private string dllPath = @"D:\DITI\CsharpProjectTest\MovieDatabase\IFN664 Assignment\bin\Debug\netcoreapp3.1\IFN664 Assignment.dll";
    private string apiKey = "MDE5ZGQ0NmEtYzcxYi03ZDY2LThhYTAtNDZmOTZhMTY5ZGFiOmVjMTA0ZDYxLTcyMTEtNGQ2Yi04OTQxLTAyNTczNTYxNDBkNQ==";

    protected override async void OnStartup(StartupEventArgs e)
    {
        AllocConsole();
        base.OnStartup(e);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var db = new AppDbContext(options);
        var dictService = new DictionaryService(db);

        // =========================================
        // ЭТАП 1: АНАЛИЗ ТЗ (МОДУЛЬ 3)
        // =========================================
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("  ЭТАП 1: АНАЛИЗ ТЗ (МОДУЛЬ 3)");
        Console.WriteLine("═══════════════════════════════════════\n");

        int specificationId;

        // 1a. Локальный анализ
        Console.WriteLine("--- Локальный анализатор ---");
        var localFactory = new ExtractorFactory(dict: dictService);
        var localExtractor = localFactory.Create(isOnline: false);
        var localSpec = await localExtractor.ExtractAsync(tzPath, specificationId: 0);
        Console.WriteLine($"Локально извлечено требований: {localSpec.Requirements.Count}");

        // 1b. AI анализ (GigaChat)
        Console.WriteLine("\n--- GigaChat анализатор ---");
        var aiFactory = new ExtractorFactory(dict: dictService, apiKey: apiKey);
        var aiExtractor = aiFactory.Create(isOnline: true);
        ProjectSpecification aiSpec;
        try
        {
            aiSpec = await aiExtractor.ExtractAsync(tzPath, specificationId: 0);
            Console.WriteLine($"GigaChat извлечено требований: {aiSpec.Requirements.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GigaChat недоступен: {ex.Message}. Использую локальный результат.");
            aiSpec = localSpec;
        }

        // Сохраняем в БД (выбираем AI-результат, если есть)
        var specService = new SpecificationService(db);
        specificationId = await specService.SaveAsync(aiSpec);
        Console.WriteLine($"\nТЗ сохранено в БД. SpecificationId: {specificationId}");

        // Добавляем Judgment-промпт для нового ТЗ, если его ещё нет
        var existingPrompt = await db.PromptTemplates
            .FirstOrDefaultAsync(p => p.SpecificationId == specificationId && p.PromptType == "Judgment");

        if (existingPrompt == null)
        {
            db.PromptTemplates.Add(new PromptTemplate
            {
                SpecificationId = specificationId,
                PromptType = "Judgment",
                SystemPrompt = "Ты — судья качества кода. Проверь, соответствует ли код стажёра требованию из ТЗ. Отвечай ТОЛЬКО на русском языке. Ответь ТОЛЬКО JSON: {\"IsPassed\": true/false, \"Reason\": \"объяснение на русском\", \"Confidence\": 0.0-1.0}",
                UserPromptTemplate = "Требование: {0}\nКод стажёра: {1}",
                IsActive = true
            });
            await db.SaveChangesAsync();
            Console.WriteLine("Judgment-промпт создан автоматически.");
        }

        // Загружаем требования для отображения
        var savedSpec = await specService.LoadAsync(specificationId);
        Console.WriteLine($"Всего требований в БД: {savedSpec?.Requirements.Count ?? 0}");
        foreach (var req in savedSpec?.Requirements ?? new())
        {
            Console.WriteLine($"  [{req.RequirementType}] {req.Severity} | {req.Title}");
        }

        // =========================================
        // ЭТАП 2: АНАЛИЗ КОДА (МОДУЛИ 4-5-6)
        // =========================================
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("  ЭТАП 2: АНАЛИЗ КОДА СТАЖЁРА");
        Console.WriteLine("═══════════════════════════════════════\n");

        if (!Directory.Exists(projectPath))
        {
            Console.WriteLine($"Папка проекта не найдена: {projectPath}");
            Console.ReadKey();
            Shutdown();
            return;
        }

        // Проверка на пустой проект
        var validator = new ProjectValidator();
        var emptyCheck = validator.CheckIfEmpty(projectPath);
        Console.WriteLine($"Проект пустой: {emptyCheck.IsEmpty}");
        Console.WriteLine($"Причина: {emptyCheck.Reason}\n");

        if (emptyCheck.IsEmpty)
        {
            Console.WriteLine("Проект пустой — анализ не требуется.");
            Console.ReadKey();
            Shutdown();
            return;
        }

        // Создаём сессию
        var roslynService = new RoslynResultService(db);
        int sessionId = await roslynService.CreateSessionAsync(
            traineeId: 1, projectId: 1,
            specificationId: specificationId,
            isAiAvailable: true);

        // --- Розлин ---
        Console.WriteLine("--- ROSLYN ---");
        var roslynAnalyzer = new RoslynSyntaxAnalyzer();
        var roslynResult = await roslynAnalyzer.AnalyzeProjectAsync(projectPath);
        Console.WriteLine($"Методов: {roslynResult.TotalMethods}");
        Console.WriteLine($"Превышений сложности: {roslynResult.MethodsExceedingComplexity}");
        Console.WriteLine($"Превышений строк: {roslynResult.MethodsExceedingLines}");
        await roslynService.SaveResultsAsync(sessionId, roslynResult);
        Console.WriteLine("Сохранено в БД.\n");

        // --- NetArchTest ---
        Console.WriteLine("--- NETARCHTEST ---");
        var archAnalyzer = new ArchitectureAnalyzer();
        var rules = archAnalyzer.LoadRules(db, projectId: 1);

        if (!rules.Any())
        {
            Console.WriteLine("Нет правил в БД. Добавьте правила в ArchRules.\n");
        }
        else if (File.Exists(dllPath))
        {
            var archViolations = archAnalyzer.Analyze(dllPath, rules);
            foreach (var v in archViolations)
                Console.WriteLine($"  [{(v.IsPassed ? "OK" : "НАРУШЕНИЕ")}] {v.RuleName}: {v.Message}");
            await roslynService.SaveNetArchResultsAsync(sessionId, archViolations);
            Console.WriteLine("Сохранено в БД.\n");
        }
        else
        {
            Console.WriteLine($"Сборка не найдена: {dllPath}\n");
        }

        // --- AI Judge ---
        Console.WriteLine("--- AI JUDGE ---");
        var gigaExtractor = new GigaChatExtractor(apiKey, dictService);
        var token = await gigaExtractor.GetAccessTokenAsync();

        var aiJudge = new AISemanticJudge(db, token);

        var allCode = string.Join("\n", Directory
            .GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
            .Select(f => File.ReadAllText(f)));

        Console.WriteLine($"Кода: {allCode.Length} символов");
        await aiJudge.JudgeAsync(sessionId, specificationId, allCode);

        var aiVerdicts = await db.AuditVerdicts
            .Where(v => v.SessionId == sessionId && v.AiModel == "GigaChat")
            .ToListAsync();
        Console.WriteLine($"AI вердиктов: {aiVerdicts.Count}");
        foreach (var v in aiVerdicts)
            Console.WriteLine($"  [{(v.IsPassed ? "OK" : "FAIL")}] {v.Reason?[..Math.Min(120, v.Reason?.Length ?? 0)]}");

        // Обновляем сессию
        await roslynService.UpdateSessionAsync(sessionId, roslynResult);

        // =========================================
        // ИТОГИ
        // =========================================
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("  ИТОГИ");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine($"SpecificationId: {specificationId}");
        Console.WriteLine($"SessionId: {sessionId}");

        var allVerdicts = await db.AuditVerdicts.Where(v => v.SessionId == sessionId).ToListAsync();
        Console.WriteLine($"Всего вердиктов: {allVerdicts.Count}");
        Console.WriteLine($"  Roslyn: {allVerdicts.Count(v => v.AiModel == "Roslyn")}");
        Console.WriteLine($"  NetArchTest: {allVerdicts.Count(v => v.AiModel == "NetArchTest")}");
        Console.WriteLine($"  GigaChat: {allVerdicts.Count(v => v.AiModel == "GigaChat")}");

        Console.WriteLine("\nНажмите любую клавишу...");
        Console.ReadKey();
        Shutdown();
    }
}