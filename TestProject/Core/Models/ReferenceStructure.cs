using DocumentFormat.OpenXml.Presentation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    // Основная информация эталонного проекта
    public class ReferenceStructure
    {
        public List<ReferenceClass> Classes { get; set; } = new();
        public int TotalClasses { get; set; }
        public int TotalMethod {  get; set; }
        public double AvgCyclomaticComplexity { get; set; }
    }

    // Информация о классе в эталонном проекте
    public class ReferenceClass
    {
        public string Namespace { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public List<ReferenceMethod> Methods { get; set; } = new();

    }

    // Метрики метода в эталонном проекте
    public class ReferenceMethod
    {
        public string MethodName { get; set; } = string.Empty;
        public int CyclomaticComplexity { get; set; }
        public int ExecutableLines { get; set; }
    }

    // Результат структурного сравнения
    public class StructureComparisonResult
    {
        public List<string> MissingClasses { get; set; } = new();
        public List<string> MissingMethods { get; set; } = new();
        public List<string> ComplexityDifferences { get; set; } = new();
        public bool IsMatching => !MissingClasses.Any() && !MissingMethods.Any() && !ComplexityDifferences.Any();
    }
}
