using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required][MaxLength(50)] public string Login { get; set; } = string.Empty;
        [Required][MaxLength(255)] public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(50)] public string? LastName { get; set; }
        [MaxLength(50)] public string? FirstName { get; set; }
        [MaxLength(50)] public string? MiddleName { get; set; }

        public int RoleId { get; set; }
        public int? DepartmentId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
