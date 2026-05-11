using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class SessionAnalysis
    {
        public int SessionId { get; set; }
        public int TraineeId { get; set; }
        public int ProjectId { get; set; }
        public int SpecificationId { get; set; }
        public int? MentorId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Status { get; set; }
        public decimal? OverallMatchPercent { get; set; }
        public bool? IsAiAvailable { get; set; }
        public bool? IsArchived { get; set; }
        public DateTime? ArchivedDate { get; set; }
        public decimal? ReferenceMatchPercent { get; set; }
    }
}
