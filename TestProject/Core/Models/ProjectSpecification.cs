using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class ProjectSpecification
    {
        public int SpecificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? ExtractedJson { get; set; }
        public string? ExtractionType { get; set; }
        public int CreatedBy { get; set; } = 1;
        public DateTime? CreatedAt { get; set; }
        public bool? IsActive { get; set; }
        public List<Requirement> Requirements { get; set; } = new();
    }
}
