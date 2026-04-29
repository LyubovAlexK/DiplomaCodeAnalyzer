using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class RequirementMarker
    {
        public int MarkerId { get; set; }
        public int SpecificationId { get; set; }
        public string Phrase { get; set; } = string.Empty;
        public string RequirementType { get; set; } = "Functional";
        public string DefaultSeverity { get; set; } = "Major";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
