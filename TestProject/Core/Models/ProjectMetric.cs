using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class ProjectMetric
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectMetricId { get; set; }

        public int ProjectId { get; set; }
        public int MetricId { get; set; }
        public decimal? CustomThreshold { get; set; }
        public bool? IsSelected { get; set; }
    }
}
