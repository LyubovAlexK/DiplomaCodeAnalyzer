using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Analyzers.Services
{
    public class RoslynSyntaxAnalyzer
    {
        public async Task<CodeAnalysisResult> AnalyzeProjectAsync(string projectPath)
        {
            var result = new CodeAnalysisResult();
            var excludeFolders = new[] { "bin", "obj", "node_modules", ".git", "packages" };

            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludeFolders.Any(ex => f.Contains($"\\{ex}\\") || f.Contains($"/{ex}/")))
                .ToArray();

            foreach (var file in csFiles)
            {
                try
                {
                    var sourceCode = await File.ReadAllTextAsync(file);
                    var tree = CSharpSyntaxTree.ParseText(sourceCode);
                    var root = await tree.GetRootAsync();

                    var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        var metrics = AnalyzeMethodFast(method, file);
                        result.Methods.Add(metrics);
                    }
                }
                catch { }
            }

            result.TotalMethods = result.Methods.Count;
            if (result.TotalMethods > 0)
            {
                result.AvgCyclomaticComplexity = result.Methods.Average(m => m.CyclomaticComplexity);
                result.AvgCognitiveComplexity = result.Methods.Average(m => m.CognitiveComplexity);
                result.AvgExecutableLines = result.Methods.Average(m => m.ExecutableLines);
                result.MethodsExceedingComplexity = result.Methods.Count(m => m.CyclomaticComplexity > 10);
                result.MethodsExceedingLines = result.Methods.Count(m => m.ExecutableLines > 20);
            }

            return result;
        }

        //Анализ одного метода — все метрики за один проход
        private MethodMetrics AnalyzeMethodFast(MethodDeclarationSyntax method, string filePath)
        {
            var className = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "Unknown";
            var methodName = method.Identifier.Text;

            // Один проход по всем узлам метода
            int cyclomatic = 1;
            int cognitive = 0;
            int executable = 0;
            int maxDepth = 0;

            foreach (var node in method.DescendantNodes())
            {
                // --- Цикломатическая сложность ---
                if (IsBranchPoint(node))
                    cyclomatic++;

                // --- Исполняемые строки ---
                if (node is StatementSyntax && node is not BlockSyntax)
                    executable++;

                // --- Когнитивная сложность + глубина вложенности ---
                if (IsBranchPoint(node) || node is CatchClauseSyntax || node is ConditionalExpressionSyntax)
                {
                    int depth = node.AncestorsAndSelf().Count(a =>
                        a is IfStatementSyntax ||
                        a is WhileStatementSyntax ||
                        a is ForStatementSyntax ||
                        a is ForEachStatementSyntax ||
                        a is CatchClauseSyntax ||
                        a is TryStatementSyntax ||
                        a is SwitchStatementSyntax);

                    cognitive += 1 + depth;

                    if (depth > maxDepth)
                        maxDepth = depth;
                }
            }

            // Плотность комментариев
            var totalLines = method.GetText().Lines.Count;
            var commentLines = method.DescendantTrivia()
                .Count(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                            t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            double commentDensity = totalLines > 0 ? (double)commentLines / totalLines * 100 : 0;

            return new MethodMetrics
            {
                MethodName = methodName,
                ClassName = className,
                FilePath = filePath,
                CyclomaticComplexity = cyclomatic,
                CognitiveComplexity = cognitive,
                ExecutableLines = executable,
                TotalLines = totalLines,
                NestingDepth = maxDepth,
                CommentDensity = commentDensity
            };
        }


        //Является ли узел точкой ветвления
        private bool IsBranchPoint(SyntaxNode node)
        {
            return node is IfStatementSyntax ||
                   node is WhileStatementSyntax ||
                   node is ForStatementSyntax ||
                   node is ForEachStatementSyntax ||
                   node is CaseSwitchLabelSyntax ||
                   node is ConditionalExpressionSyntax ||
                   node is CatchClauseSyntax ||
                   (node is BinaryExpressionSyntax binary &&
                       (binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                        binary.IsKind(SyntaxKind.LogicalOrExpression)));
        }

        // Анализ эталонного проекта
        public async Task<ReferenceStructure> AnalyzeProjectStructureAsync(string projectPath)
        {
            var structure = new ReferenceStructure();
            var excludeFolders = new[] { "bin", "obj", "node_modules", ".git", "packages" };
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludeFolders.Any(ex => f.Contains($"\\{ex}\\") || f.Contains($"/{ex}/")))
                .ToArray();

            int totalMethods = 0;
            double totalComplexity = 0;

            foreach (var file in csFiles)
            {
                var sourceCode = await File.ReadAllTextAsync(file);
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = await tree.GetRootAsync();

                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var @class in classes)
                {
                    //Защита от зависимости версий (слева null)
                    var ns = @class.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                        ?? @class.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
                        ?? "Global";
                    var refClass = new ReferenceClass
                    {
                        Namespace = ns,
                        ClassName = @class.Identifier.Text
                    };

                    var methods = @class.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        var metrics = AnalyzeMethodFast(method, file);
                        refClass.Methods.Add(new ReferenceMethod
                        { 
                            MethodName = method.Identifier.Text,
                            CyclomaticComplexity = metrics.CyclomaticComplexity,
                            ExecutableLines = metrics.ExecutableLines
                        });

                        totalMethods++;
                        totalComplexity += metrics.CyclomaticComplexity;
                    }
                    structure.Classes.Add(refClass);
                }
            }

            structure.TotalClasses = structure.Classes.Count;
            structure.TotalMethod = totalMethods;
            structure.AvgCyclomaticComplexity = totalComplexity > 0 ? totalComplexity / totalMethods : 0;
        
            return structure;
        }
    }
}
