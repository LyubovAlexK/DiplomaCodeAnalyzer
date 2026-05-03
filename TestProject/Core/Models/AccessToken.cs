using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class AccessToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TokenId { get; set; }

        [Required]
        [MaxLength(500)]
        public string TokenValue { get; set; } = string.Empty;

        public string? Description { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool? IsActive { get; set; }
    }
}
