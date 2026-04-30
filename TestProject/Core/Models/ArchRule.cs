using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class ArchRule
    {
        public int RuleId { get; set; }
        public int ProjectId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string RuleJson { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool? IsActive { get; set; }
    }
}
