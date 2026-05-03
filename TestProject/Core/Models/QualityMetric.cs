using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class QualityMetric
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MetricId { get; set; }

        [Required]
        [MaxLength(100)]
        public string MetricName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string MetricType { get; set; } = string.Empty; // Roslyn, NetArchTest, AI

        public decimal? DefaultThreshold { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}
