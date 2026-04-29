using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class PromptTemplate
    {
        public int PromptId { get; set; }
        public int SpecificationId { get; set; }
        public string PromptType { get; set; } = "Extraction"; // Extraction / Judgment
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPromptTemplate { get; set; } = string.Empty; // {0}, {1}
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
