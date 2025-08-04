using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Models.Entities
{
    public class ChatSession : BaseEntity
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Title { get; set; } = "New Chat";

        // Navigation properties
        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
