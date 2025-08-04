using AICodePortal.Backend.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Models.Entities
{
    public class ChatMessage : BaseEntity
    {
        [Required]
        public int ChatSessionId { get; set; }

        [Required]
        public string UserMessage { get; set; } = string.Empty;

        [Required]
        public string AiResponse { get; set; } = string.Empty;

        public string? RelevantFiles { get; set; } // JSON string
        public string? ProjectStructure { get; set; } // JSON string
        public TimeSpan ProcessingTime { get; set; }

        // Navigation properties
        public virtual ChatSession ChatSession { get; set; } = null!;
    }
}
