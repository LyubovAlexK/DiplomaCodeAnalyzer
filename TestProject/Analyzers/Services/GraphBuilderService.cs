using Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

namespace Analyzers.Services;

public class GraphBuilderService
{
    /// <summary>
    /// Строит граф зависимостей проекта.
    /// </summary>
    public async Task<ProjectGraph> BuildGraphAsync(string projectPath)
    {
        var graph = new ProjectGraph();
        var excludeFolders = new[] { "bin", "obj", "node_modules", ".git", "packages" };

        var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !excludeFolders.Any(ex => f.Contains($"\\{ex}\\") || f.Contains($"/{ex}/")))
            .ToArray();

        var nodeDict = new Dictionary<string, GraphNode>();
        var edgeSet = new HashSet<(string, string)>();

        foreach (var file in csFiles)
        {
            var sourceCode = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = await tree.GetRootAsync();

            // Извлекаем классы
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var @class in classes)
            {
                var ns = GetNamespace(@class);
                var fullName = $"{ns}.{@class.Identifier.Text}";
                var methods = @class.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

                if (!nodeDict.ContainsKey(fullName))
                {
                    nodeDict[fullName] = new GraphNode
                    {
                        Id = fullName,
                        Name = @class.Identifier.Text,
                        FullName = fullName,
                        Kind = "Class",
                        Namespace = ns,
                        FilePath = file,
                        LineNumber = @class.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        MethodCount = methods.Count
                    };
                }

                // Извлекаем зависимости класса
                ExtractClassDependencies(@class, fullName, nodeDict, edgeSet, file);
            }

            // Извлекаем интерфейсы
            var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
            foreach (var iface in interfaces)
            {
                var ns = GetNamespace(iface);
                var fullName = $"{ns}.{iface.Identifier.Text}";
                if (!nodeDict.ContainsKey(fullName))
                {
                    nodeDict[fullName] = new GraphNode
                    {
                        Id = fullName,
                        Name = iface.Identifier.Text,
                        FullName = fullName,
                        Kind = "Interface",
                        Namespace = ns,
                        FilePath = file
                    };
                }
            }
        }

        graph.Nodes = nodeDict.Values.ToList();
        graph.Edges = edgeSet.Select(e => new GraphEdge { SourceId = e.Item1, TargetId = e.Item2, RelationType = "USES" }).ToList();
        graph.TotalClasses = nodeDict.Values.Count(n => n.Kind == "Class");
        graph.TotalMethods = nodeDict.Values.Sum(n => n.MethodCount);
        graph.CyclicDependencies = FindCycles(graph);

        // Определяем категории классов
        foreach (var node in graph.Nodes)
        {
            node.Category = DetermineCategory(node);
        }

        return graph;
    }

    private void ExtractClassDependencies(ClassDeclarationSyntax @class, string classFullName,
        Dictionary<string, GraphNode> nodeDict, HashSet<(string, string)> edgeSet, string file)
    {
        // Наследование (EXTENDS)
        if (@class.BaseList != null)
        {
            foreach (var baseType in @class.BaseList.Types)
            {
                var baseName = baseType.Type.ToString();
                if (!nodeDict.ContainsKey(baseName))
                {
                    nodeDict[baseName] = new GraphNode
                    {
                        Id = baseName,
                        Name = baseName,
                        FullName = baseName,
                        Kind = "Class",
                        FilePath = file
                    };
                }
                edgeSet.Add((classFullName, baseName));
            }
        }

        // Поля и свойства (USES_FIELD)
        var fields = @class.DescendantNodes().OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            var typeName = field.Declaration.Type.ToString();
            AddEdgeIfType(classFullName, typeName, nodeDict, edgeSet, file);
        }

        var properties = @class.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            var typeName = prop.Type.ToString();
            AddEdgeIfType(classFullName, typeName, nodeDict, edgeSet, file);
        }

        // Вызовы методов внутри методов класса (CALLS)
        var methods = @class.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var inv in invocations)
            {
                var calledName = inv.Expression.ToString().Split('.').Last();
                if (nodeDict.ContainsKey(calledName))
                    edgeSet.Add((classFullName, calledName));
            }
        }
    }

    private void AddEdgeIfType(string sourceId, string typeName,
    Dictionary<string, GraphNode> nodeDict, HashSet<(string, string)> edgeSet, string file)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return;
        if (typeName.Contains('<')) return; // дженерики

        // ФИЛЬТР: игнорируем примитивы и системные типы
        if (IsPrimitiveOrSystemType(typeName)) return;

        if (!nodeDict.ContainsKey(typeName))
        {
            nodeDict[typeName] = new GraphNode
            {
                Id = typeName,
                Name = typeName,
                FullName = typeName,
                Kind = "Class",
                FilePath = file
            };
        }
        edgeSet.Add((sourceId, typeName));
    }

    private bool IsPrimitiveOrSystemType(string typeName)
    {
        // Примитивы
        var primitives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "string", "int", "long", "short", "byte", "float", "double", "decimal",
        "bool", "char", "object", "void", "DateTime", "TimeSpan", "Guid",
        "Int32", "Int64", "Int16", "Boolean", "Decimal", "Double", "Single",
        "String", "Char", "Byte", "Object", "Void"
    };

        if (primitives.Contains(typeName)) return true;

        // Системные пространства имён
        var systemNamespaces = new[]
        {
        "System.", "Microsoft.", "System.Collections", "System.Linq",
        "System.Threading", "System.IO", "System.Net", "System.Text",
        "System.Xml", "Newtonsoft", "DocumentFormat", "UglyToad",
        "OxyPlot", "LiveCharts", "Catalyst", "Mosaik", "BCrypt",
        "Microsoft.EntityFrameworkCore", "MessagePack", "Nito"
    };

        foreach (var ns in systemNamespaces)
        {
            if (typeName.StartsWith(ns, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Дженерики: List<T>, Dictionary<K,V> и т.п.
        if (typeName.Contains('<') && typeName.Contains('>'))
            return true;

        return false;
    }

    private string GetNamespace(SyntaxNode node)
    {
        return node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
            ?? node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
            ?? "Global";
    }

    /// <summary>
    /// Поиск циклических зависимостей (алгоритм обнаружения циклов в графе).
    /// </summary>
    private List<string> FindCycles(ProjectGraph graph)
    {
        var cycles = new List<string>();
        var adj = new Dictionary<string, List<string>>();
        foreach (var edge in graph.Edges)
        {
            if (!adj.ContainsKey(edge.SourceId)) adj[edge.SourceId] = new();
            adj[edge.SourceId].Add(edge.TargetId);
        }

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in graph.Nodes)
        {
            if (DFS(node.Id, adj, visited, recursionStack, new List<string>(), cycles))
                break;
        }

        return cycles.Distinct().ToList();
    }

    private bool DFS(string node, Dictionary<string, List<string>> adj,
        HashSet<string> visited, HashSet<string> recursionStack, List<string> path, List<string> cycles)
    {
        visited.Add(node);
        recursionStack.Add(node);
        path.Add(node);

        if (adj.ContainsKey(node))
        {
            foreach (var neighbor in adj[node])
            {
                if (!visited.Contains(neighbor))
                {
                    if (DFS(neighbor, adj, visited, recursionStack, path, cycles))
                        return true;
                }
                else if (recursionStack.Contains(neighbor))
                {
                    var cycleStart = path.IndexOf(neighbor);
                    cycles.Add(string.Join(" → ", path.Skip(cycleStart)) + $" → {neighbor}");
                }
            }
        }

        recursionStack.Remove(node);
        path.RemoveAt(path.Count - 1);
        return false;
    }
    private string DetermineCategory(GraphNode node)
    {
        var name = node.FullName.ToLower();
        var ns = node.Namespace.ToLower();

        // UI / Presentation
        if (ns.Contains("view") || ns.Contains("page") || ns.Contains("window") ||
            ns.Contains("control") || ns.Contains("ui") || ns.Contains("presentation") ||
            name.Contains("view") || name.Contains("page") || name.Contains("window"))
            return "UI";

        // Data Access / Repositories
        if (ns.Contains("data") || ns.Contains("repository") || ns.Contains("dal") ||
            ns.Contains("dbcontext") || name.Contains("repository") ||
            name.Contains("dbcontext") || name.Contains("datacontext") ||
            name.Contains("service") && (ns.Contains("data") || ns.Contains("infrastructure")))
            return "DataAccess";

        // Business Logic / Services
        if (ns.Contains("service") || ns.Contains("business") || ns.Contains("bll") ||
            ns.Contains("logic") || ns.Contains("domain") ||
            name.Contains("service") || name.Contains("manager") || name.Contains("controller"))
            return "Business";

        // Data Models
        if (node.Kind == "Class" && node.MethodCount <= 3 &&
            (ns.Contains("model") || ns.Contains("entity") || ns.Contains("domain") ||
             ns.Contains("dto") || name.Contains("model") || name.Contains("entity")))
            return "Data";

        // Interfaces
        if (node.Kind == "Interface")
            return "Interface";

        return "Other";
    }
}