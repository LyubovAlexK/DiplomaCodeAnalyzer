using System.IO;
using System.Reflection;
using Core.Models;

namespace Analyzers.Services;

public class ArchitectureAnalyzer
{
    public List<ArchViolation> Analyze(string dllPath, List<ArchRule> rules)
    {
        var violations = new List<ArchViolation>();

        if (!File.Exists(dllPath))
        {
            violations.Add(new ArchViolation
            {
                RuleName = "Сборка не найдена",
                Message = $"Файл не существует: {dllPath}",
                IsPassed = false
            });
            return violations;
        }

        if (!rules.Any())
        {
            violations.Add(new ArchViolation
            {
                RuleName = "Нет правил",
                Message = "Нет активных архитектурных правил.",
                IsPassed = true
            });
            return violations;
        }

        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            Console.WriteLine($"Загружено типов: {types.Length}");
            foreach (var t in types)
                Console.WriteLine($"  {t.Namespace}.{t.Name}");

            foreach (var rule in rules)
            {
                if (rule.RuleJson.Contains("namespace"))
                {
                    var parts = rule.RuleJson.Split(';');
                    var sourceNs = parts[0].Replace("namespace:", "").Trim();
                    var forbiddenNs = parts.Length > 1 ? parts[1].Replace("forbidden:", "").Trim() : null;

                    var sourceTypes = types
                        .Where(t => t.Namespace?.StartsWith(sourceNs) == true)
                        .ToList();

                    if (forbiddenNs != null && sourceTypes.Any())
                    {
                        foreach (var type in sourceTypes)
                        {
                            var referencedTypes = GetReferencedTypes(type);
                            var forbiddenDeps = referencedTypes
                                .Where(t => t.Namespace?.StartsWith(forbiddenNs) == true)
                                .ToList();

                            if (forbiddenDeps.Any())
                            {
                                violations.Add(new ArchViolation
                                {
                                    RuleName = rule.RuleName,
                                    Message = $"Класс {type.Name} зависит от {forbiddenDeps[0].Namespace}",
                                    IsPassed = false
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            violations.Add(new ArchViolation
            {
                RuleName = "Ошибка анализа",
                Message = ex.Message,
                IsPassed = false
            });
        }

        if (!violations.Any())
        {
            violations.Add(new ArchViolation
            {
                RuleName = "Все правила",
                Message = "Архитектурные правила соблюдены.",
                IsPassed = true
            });
        }

        return violations;
    }

    public List<ArchRule> LoadRules(Core.Data.AppDbContext db, int projectId)
    {
        return db.ArchRules
            .Where(r => r.ProjectId == projectId && r.IsActive == true)
            .ToList();
    }

    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        var referencedTypes = new HashSet<Type>();

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            referencedTypes.Add(field.FieldType);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            referencedTypes.Add(prop.PropertyType);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            referencedTypes.Add(method.ReturnType);
            foreach (var param in method.GetParameters())
                referencedTypes.Add(param.ParameterType);
        }

        if (type.BaseType != null && type.BaseType != typeof(object))
            referencedTypes.Add(type.BaseType);

        return referencedTypes;
    }
}

public class ArchViolation
{
    public string RuleName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsPassed { get; set; }
}