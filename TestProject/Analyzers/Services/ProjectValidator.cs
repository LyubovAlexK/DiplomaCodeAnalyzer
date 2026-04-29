using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzers.Services
{
    public class ProjectValidator
    {
        /// <summary>
        /// Проверяет, является ли проект пустым.
        /// Уровни проверки:
        /// 1. Файловая структура (исключаем bin/obj/GeneratedCode)
        /// 2. Пользовательские типы (не шаблоны)
        /// 3. Исполняемая логика (значимые операторы)
        /// 4. Сравнение с шаблонами dotnet new
        /// </summary>
        public EmptyProjectResult CheckIfEmpty(string projectPath)
        {
            var excludeFolders = new[] { "bin", "obj", "node_modules", ".git", "packages" };
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludeFolders.Any(ex => f.Contains($"\\{ex}\\") || f.Contains($"/{ex}/")))
                .ToArray();

            // ===== Уровень 1: Файловая структура =====
            if (csFiles.Length == 0)
            {
                return new EmptyProjectResult
                {
                    IsEmpty = true,
                    Reason = "Уровень 1: Не найдено ни одного .cs файла.",
                    Confidence = 1.0,
                    FailLevel = 1
                };
            }

            int totalMethods = 0;
            int meaningfulMethods = 0;
            int totalClasses = 0;
            int userClasses = 0;
            int templateMatches = 0;

            foreach (var file in csFiles)
            {
                try
                {
                    var sourceCode = File.ReadAllText(file);
                    var tree = CSharpSyntaxTree.ParseText(sourceCode);
                    var root = tree.GetRoot();

                    // Исключаем файлы с GeneratedCodeAttribute
                    if (IsGeneratedCode(root))
                        continue;

                    // ===== Уровень 2: Пользовательские типы =====
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
                    totalClasses += classes.Count;

                    foreach (var @class in classes)
                    {
                        if (IsUserClass(@class))
                            userClasses++;
                    }

                    // Если нет ни одного класса — проверяем другие типы (интерфейсы, структуры)
                    var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
                    if (typeDeclarations.Count == 0)
                        continue;

                    // ===== Уровень 3: Исполняемая логика =====
                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        totalMethods++;

                        if (HasMeaningfulCode(method))
                            meaningfulMethods++;
                    }

                    // ===== Уровень 4: Сравнение с шаблонами dotnet new =====
                    if (IsTemplateMatch(root, sourceCode))
                        templateMatches++;

                }
                catch { }
            }

            // ===== Принятие решения =====
            var result = new EmptyProjectResult
            {
                TotalMethods = totalMethods,
                TotalClasses = totalClasses,
                UserClasses = userClasses,
                MeaningfulMethods = meaningfulMethods,
                TemplateMatches = templateMatches,
                TotalFiles = csFiles.Length
            };

            // Уровень 1 уже проверен (csFiles.Length > 0)

            // Уровень 2: Нет пользовательских типов
            if (userClasses == 0)
            {
                result.IsEmpty = true;
                result.Reason = $"Уровень 2: Нет пользовательских классов (всего классов: {totalClasses}, из них шаблонных/сгенерированных: {totalClasses - userClasses}).";
                result.Confidence = 0.95;
                result.FailLevel = 2;
                return result;
            }

            // Уровень 3: Нет значимых методов
            if (meaningfulMethods == 0)
            {
                result.IsEmpty = true;
                result.Reason = $"Уровень 3: Все {totalMethods} методов пустые или содержат только вызовы базового конструктора.";
                result.Confidence = 0.9;
                result.FailLevel = 3;
                return result;
            }

            // Уровень 4: Все файлы — шаблоны
            if (templateMatches == csFiles.Length)
            {
                result.IsEmpty = true;
                result.Reason = $"Уровень 4: Все {csFiles.Length} файлов совпадают с шаблонами dotnet new (немодифицированные).";
                result.Confidence = 0.85;
                result.FailLevel = 4;
                return result;
            }

            // Всё хорошо
            result.IsEmpty = false;
            result.Reason = $"Проект не пустой. Найдено {userClasses} пользовательских классов, {meaningfulMethods} значимых методов из {totalMethods}.";
            result.Confidence = 1.0;
            result.FailLevel = 0;
            return result;
        }

        /// <summary>
        /// Проверяет, помечен ли файл атрибутом GeneratedCode.
        /// </summary>
        private bool IsGeneratedCode(SyntaxNode root)
        {
            return root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .Any(a => a.Name.ToString().Contains("GeneratedCode") ||
                          a.Name.ToString().Contains("GeneratedCodeAttribute"));
        }

        /// <summary>
        /// Является ли класс пользовательским (не шаблоном, не сгенерированным).
        /// </summary>
        private bool IsUserClass(ClassDeclarationSyntax @class)
        {
            var name = @class.Identifier.Text;

            // Исключаем типичные шаблонные имена
            var templateNames = new[] { "Program", "Startup", "AssemblyInfo", "Resources", "Settings" };

            if (templateNames.Contains(name))
            {
                // Проверяем, есть ли в классе пользовательские методы (не Main, не конструктор)
                var methods = @class.Members.OfType<MethodDeclarationSyntax>();
                var hasCustomMethods = methods.Any(m =>
                    m.Identifier.Text != "Main" &&
                    !IsConstructor(m) &&
                    HasMeaningfulCode(m));

                return hasCustomMethods;
            }

            return true;
        }

        /// <summary>
        /// Содержит ли метод значимый код (не пустой, не только вызов базового конструктора).
        /// </summary>
        private bool HasMeaningfulCode(MethodDeclarationSyntax method)
        {
            if (method.Body == null && method.ExpressionBody == null)
                return false;

            if (method.Body != null)
            {
                var statements = method.Body.Statements;

                // Пустое тело
                if (statements.Count == 0)
                    return false;

                // Только throw new NotImplementedException()
                if (statements.Count == 1 &&
                    statements[0].ToString().Contains("throw new NotImplementedException()"))
                    return false;

                // Только вызов базового конструктора или InitializeComponent
                if (statements.Count == 1)
                {
                    var text = statements[0].ToString();
                    if (text.Contains("InitializeComponent()") && method.Identifier.Text == method.Identifier.Text)
                    {
                        // Проверяем, есть ли другие методы в классе с логикой
                        var parentClass = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                        if (parentClass != null)
                        {
                            var otherMethods = parentClass.Members.OfType<MethodDeclarationSyntax>()
                                .Where(m => m != method);
                            if (otherMethods.Any(m => HasMeaningfulCode(m)))
                                return false; // Конструктор с InitializeComponent, но есть другие методы
                        }
                        return false; // Только конструктор с InitializeComponent
                    }
                    if (text.Contains("base.") && statements[0] is ExpressionStatementSyntax)
                        return false;
                }

                return true;
            }

            // Expression body (напр., => ...)
            if (method.ExpressionBody != null)
            {
                var text = method.ExpressionBody.ToString();
                if (text.Contains("throw new NotImplementedException()"))
                    return false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Является ли метод конструктором.
        /// </summary>
        private bool IsConstructor(MethodDeclarationSyntax method)
        {
            return method.Identifier.Text == method.Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault()?.Identifier.Text;
        }

        /// <summary>
        /// Проверяет, совпадает ли файл с шаблоном dotnet new.
        /// Сравнивает структуру AST с эталонным Program.cs.
        /// </summary>
        private bool IsTemplateMatch(SyntaxNode root, string sourceCode)
        {
            // Эталонный Program.cs для .NET 6+ (top-level statements)
            if (root.DescendantNodes().OfType<GlobalStatementSyntax>().Any())
            {
                var statements = root.DescendantNodes().OfType<GlobalStatementSyntax>().ToList();
                if (statements.Count == 1)
                {
                    var text = statements[0].ToString();
                    if (text.Contains("See https://aka.ms/new-console-template") ||
                        text.Contains("Console.WriteLine(\"Hello, World!\")") ||
                        text.Trim() == "Console.WriteLine(\"Hello, World!\");")
                    {
                        return true;
                    }
                }
                // Две строки: комментарий + Console.WriteLine
                if (statements.Count == 2)
                {
                    var first = statements[0].ToString();
                    var second = statements[1].ToString();
                    if (first.Contains("See https://aka.ms") &&
                        second.Contains("Console.WriteLine(\"Hello"))
                    {
                        return true;
                    }
                }
            }

            // Эталонный Program.cs для .NET 5- (с классом и Main)
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            if (classes.Count == 1 && classes[0].Identifier.Text == "Program")
            {
                var methods = classes[0].Members.OfType<MethodDeclarationSyntax>().ToList();
                if (methods.Count == 1 && methods[0].Identifier.Text == "Main")
                {
                    var body = methods[0].Body?.ToString() ?? "";
                    if (body.Contains("Console.WriteLine(\"Hello") ||
                        body.Contains("Application.Run") ||
                        body.Contains("CreateHostBuilder"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsEmptyMethod(MethodDeclarationSyntax method)
        {
            if (method.Body == null && method.ExpressionBody == null)
                return true;

            if (method.Body != null)
            {
                var statements = method.Body.Statements;
                return statements.Count == 0;
            }

            return false;
        }

        private bool IsNotImplementedMethod(MethodDeclarationSyntax method)
        {
            if (method.Body != null)
            {
                var statements = method.Body.Statements;
                if (statements.Count == 1)
                {
                    var statement = statements[0].ToString();
                    return statement.Contains("throw new NotImplementedException()");
                }
            }
            return false;
        }

        /// <summary>
        /// Проверяет, компилируется ли проект.
        /// Возвращает список ошибок компиляции.
        /// </summary>
        public CompilationResult CheckCompilation(string projectPath)
        {
            var excludeFolders = new[] { "bin", "obj", "node_modules", ".git", "packages" };
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludeFolders.Any(ex => f.Contains($"\\{ex}\\") || f.Contains($"/{ex}/")))
                .ToArray();

            if (csFiles.Length == 0)
            {
                return new CompilationResult
                {
                    CanCompile = false,
                    ErrorCount = 1,
                    Errors = new List<string> { "Нет файлов для компиляции." }
                };
            }

            var syntaxTrees = new List<SyntaxTree>();
            foreach (var file in csFiles)
            {
                try
                {
                    var sourceCode = File.ReadAllText(file);
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(sourceCode));
                }
                catch { }
            }

            // Создаём компиляцию с базовыми ссылками
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();

            var compilation = CSharpCompilation.Create(
                "ProjectCheck",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var diagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            var errors = diagnostics.Select(d =>
            {
                var location = d.Location.GetLineSpan();
                return $"[{location.Path?.Split('\\').Last() ?? "?"}:{location.StartLinePosition.Line + 1}] {d.GetMessage()}";
            }).ToList();

            return new CompilationResult
            {
                CanCompile = errors.Count == 0,
                ErrorCount = errors.Count,
                WarningCount = compilation.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Warning),
                Errors = errors
            };
        }
    }

    /// <summary>
    /// Результат проверки на пустой проект.
    /// </summary>
    public class EmptyProjectResult
    {
        public bool IsEmpty { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int TotalMethods { get; set; }
        public int TotalClasses { get; set; }
        public int UserClasses { get; set; }
        public int MeaningfulMethods { get; set; }
        public int TemplateMatches { get; set; }
        public int TotalFiles { get; set; }
        public int FailLevel { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Результат проверки компиляции.
    /// </summary>
    public class CompilationResult
    {
        public bool CanCompile { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}

