using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class CommitHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CommitId { get; set; }

        public int SessionId { get; set; }

        [Required]
        [MaxLength(40)]
        public string CommitHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AuthorName { get; set; }

        public DateTime CommitDate { get; set; }

        public int? LinesAdded { get; set; }
        public int? LinesDeleted { get; set; }
        public bool? IsAnomaly { get; set; }
    }
}
