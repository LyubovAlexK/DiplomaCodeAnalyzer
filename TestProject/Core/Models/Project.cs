using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int TraineeId { get; set; }
        public int SpecificationId { get; set; }
        public string? RepoUrl { get; set; }
        public int? TokenId { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool? IsCompleted { get; set; }
        public bool? IsArchived { get; set; }
        public DateTime? ArchivedDate { get; set; }
    }
}
