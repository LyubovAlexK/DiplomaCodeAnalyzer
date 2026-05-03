using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class ReferenceProject
    {
        public int ReferenceId { get; set; }
        public int SpecificationId { get; set; }
        public string ProjectPath { get; set; } = string.Empty;
        public string? ExtractedJson { get; set; }
        public int? TotalClasses { get; set; }
        public int? TotalMethods { get; set; }
        public decimal? AvgCyclomaticComplexity { get; set; }
        public decimal? AvgCognitiveComplexity { get; set; }
        public decimal? AvgExecutableLines { get; set; }
        public int? MethodsExceedingComplexity { get; set; }
        public int? MethodsExceedingLines { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
