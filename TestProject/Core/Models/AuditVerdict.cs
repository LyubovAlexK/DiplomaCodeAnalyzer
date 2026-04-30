using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class AuditVerdict
    {
        public int VerdictId { get; set; }
        public int SessionId { get; set; }
        public int RequirementId { get; set; }
        public bool IsPassed { get; set; }
        public string? Reason { get; set; }
        public decimal? Confidence { get; set; }
        public string? CodeLocation { get; set; }
        public string? AiModel { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
