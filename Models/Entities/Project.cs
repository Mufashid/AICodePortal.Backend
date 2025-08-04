using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Models.Entities
{
    public class Project : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string RepositoryUrl { get; set; } = string.Empty;

        [Required]
        public RepositoryType RepositoryType { get; set; } = RepositoryType.Git;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public ProjectStatus Status { get; set; } = ProjectStatus.Active;
        public DateTime? LastSyncedAt { get; set; }
        public string? LocalPath { get; set; }

        // Navigation properties
        public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    }

    public enum RepositoryType
    {
        Git = 1,
        SVN = 2
    }

    public enum ProjectStatus
    {
        Active = 1,
        Inactive = 2,
        Error = 3
    }
}