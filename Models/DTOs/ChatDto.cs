using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Models.DTOs
{
    public class CodeAnalysisRequest
    {
        [Required]
        public string Query { get; set; } = string.Empty;

        [Required]
        public string ProjectName { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        // New: Allow user to choose AI provider
        public string? AIProvider { get; set; } // "Claude", "OpenAI", or null (use default)

        // New: Allow user to choose specific model
        public string? Model { get; set; } // Optional: override default model
    }

    public class CodeAnalysisResponse
    {
        public string Response { get; set; } = string.Empty;
        public List<string> RelevantFiles { get; set; } = new List<string>();
        public string ProjectStructure { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }

        // New: Show which provider was used
        public string UsedProvider { get; set; } = string.Empty;
        public string UsedModel { get; set; } = string.Empty;
        public decimal? EstimatedCost { get; set; } // Optional: show estimated cost
    }

    public class ChatSessionDto
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int MessageCount { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string UserMessage { get; set; } = string.Empty;
        public string AiResponse { get; set; } = string.Empty;
        public List<string> RelevantFiles { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    // ========================================================================================
    // NEW: Multi-AI Provider DTOs
    // ========================================================================================

    public class AvailableProvidersResponse
    {
        public string DefaultProvider { get; set; } = string.Empty;
        public List<AIProviderInfo> Providers { get; set; } = new List<AIProviderInfo>();
    }

    public class AIProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public List<AIModelInfo> Models { get; set; } = new List<AIModelInfo>();
        public string Status { get; set; } = string.Empty; // "Healthy", "Unhealthy", "Not Configured"
    }

    public class AIModelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal EstimatedCostPer1KTokens { get; set; }
        public bool IsDefault { get; set; }
    }

    public class CreateChatSessionRequest
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public string SessionId { get; set; } = string.Empty;

        public string Title { get; set; } = "New Chat";
    }
}