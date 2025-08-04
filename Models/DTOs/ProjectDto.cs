using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Models.DTOs
{
    public class ProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public string RepositoryType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSyncedAt { get; set; }
        public int ChatSessionsCount { get; set; }
    }

    public class CreateProjectDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        [Url]
        [MaxLength(500)]
        public string RepositoryUrl { get; set; } = string.Empty;

        [Required]
        public string RepositoryType { get; set; } = "Git";

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateProjectDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
    }
}