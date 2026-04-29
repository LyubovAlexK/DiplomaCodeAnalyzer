using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class MethodMetrics
    {
        public string MethodName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int CyclomaticComplexity { get; set; }
        public int CognitiveComplexity { get; set; }
        public int ExecutableLines { get; set; }
        public int TotalLines { get; set; }
        public int NestingDepth { get; set; }
        public double CommentDensity { get; set; }
    }
    public class CodeAnalysisResult
    {
        public List<MethodMetrics> Methods { get; set; } = new();
        public int TotalMethods { get; set; }
        public double AvgCyclomaticComplexity { get; set; }
        public double AvgCognitiveComplexity { get; set; }
        public double AvgExecutableLines { get; set; }
        public int MethodsExceedingComplexity { get; set; }
        public int MethodsExceedingLines { get; set; }
    }
}
