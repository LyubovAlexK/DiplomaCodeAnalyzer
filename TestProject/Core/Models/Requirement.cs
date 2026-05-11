using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class Requirement
    {
        public int RequirementId { get; set; }

        public int SpecificationId { get; set; }

        public string RequirementType { get; set; } = string.Empty;

        public string? Category { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Severity { get; set; } = "Major";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? AcceptanceCriteria { get; set; }

        public string? ExpectedBehavior { get; set; }

        public decimal? Threshold { get; set; }

        public string? MetricName { get; set; }

        public string? TargetNamespace { get; set; }

        public string? ForbiddenDependency { get; set; }

        public bool? IsActive { get; set; }

    }   

}
