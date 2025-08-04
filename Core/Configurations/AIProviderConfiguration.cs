namespace AICodePortal.Backend.Core.Configurations
{
    public class AIProviderConfiguration
    {
        public string DefaultProvider { get; set; } = "Claude"; // "Claude" or "OpenAI"
        public bool AllowUserToChoose { get; set; } = true;
        public OpenAIConfiguration OpenAI { get; set; } = new();
        public ClaudeConfiguration Claude { get; set; } = new();
    }

    public class OpenAIConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4";
        public int MaxTokens { get; set; } = 4000;
        public double Temperature { get; set; } = 0.7;
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
    }

    public class ClaudeConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "claude-3-haiku-20240307";
        public int MaxTokens { get; set; } = 4000;
        public double Temperature { get; set; } = 0.7;
        public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
        public string AnthropicVersion { get; set; } = "2023-06-01";
    }

    public class RepositoryConfiguration
    {
        public string BasePath { get; set; } = string.Empty;
        public long MaxFileSize { get; set; } = 1048576; // 1MB
        public List<string> AllowedExtensions { get; set; } = new List<string>();
        public int MaxFilesPerAnalysis { get; set; } = 100;
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}