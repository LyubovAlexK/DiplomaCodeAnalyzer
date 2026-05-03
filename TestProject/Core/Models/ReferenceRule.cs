using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class ReferenceRule
    {
        public int RuleId { get; set; }
        public int ReferenceId { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string ThresholdType { get; set; } = "Range";
        public decimal? ThresholdValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
