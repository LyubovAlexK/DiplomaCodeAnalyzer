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

        public string ExtractionType { get; set; } = "Local";

        public List<Requirement> Requirements { get; set; } = new();

        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    }
}
