using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class AuditVerdict
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VerdictId { get; set; }

        public int SessionId { get; set; }
        public int? RequirementId { get; set; }
        public bool IsPassed { get; set; }
        public string? Reason { get; set; }
        public decimal? Confidence { get; set; }
        public string? CodeLocation { get; set; }
        public string? AiModel { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime CreatedAt { get; set; }
    }
}
