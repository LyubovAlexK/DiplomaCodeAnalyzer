using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class LearningMetric
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LearnId { get; set; }

        public int SessionId { get; set; }
        public int TraineeId { get; set; }
        public int ProjectId { get; set; }
        public int? AttemptsCount { get; set; }
        public int? TimeToProficiency { get; set; }
        public decimal? TechDebtIndex { get; set; }
        public bool? PlagiarismFlag { get; set; }
        public bool? IsArchived { get; set; }
        public DateTime? ArchivedDate { get; set; }
        public DateTime? CalculatedDate { get; set; }
    }
}
