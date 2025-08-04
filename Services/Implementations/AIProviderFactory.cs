using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Core.Configurations;
using Microsoft.Extensions.Options;

namespace AICodePortal.Backend.Services.Implementations
{
    public class AIProviderFactory : IAIProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AIProviderConfiguration _config;
        private readonly ILogger<AIProviderFactory> _logger;

        public AIProviderFactory(
            IServiceProvider serviceProvider,
            IOptions<AIProviderConfiguration> config,
            ILogger<AIProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config.Value;
            _logger = logger;
        }

        public IAIService GetProvider(string? providerName = null)
        {
            var selectedProvider = providerName ?? _config.DefaultProvider;

            try
            {
                return selectedProvider.ToUpper() switch
                {
                    "OPENAI" => _serviceProvider.GetRequiredService<OpenAIService>(),
                    "CLAUDE" => _serviceProvider.GetRequiredService<ClaudeAIService>(),
                    _ => throw new ArgumentException($"Unknown AI provider: {selectedProvider}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get AI provider: {ProviderName}", selectedProvider);
                throw;
            }
        }

        public async Task<AvailableProvidersResponse> GetAvailableProvidersAsync()
        {
            var providers = new List<AIProviderInfo>();

            // Check OpenAI
            try
            {
                var openAIService = _serviceProvider.GetRequiredService<OpenAIService>();
                var isOpenAIAvailable = await openAIService.IsServiceAvailableAsync();
                var openAIModels = await openAIService.GetAvailableModelsAsync();

                providers.Add(new AIProviderInfo
                {
                    Name = "OpenAI",
                    DisplayName = "OpenAI GPT",
                    IsAvailable = isOpenAIAvailable,
                    Models = openAIModels,
                    Status = isOpenAIAvailable ? "Healthy" : "Unhealthy"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OpenAI availability");
                providers.Add(new AIProviderInfo
                {
                    Name = "OpenAI",
                    DisplayName = "OpenAI GPT",
                    IsAvailable = false,
                    Status = "Not Configured"
                });
            }

            // Check Claude
            try
            {
                var claudeService = _serviceProvider.GetRequiredService<ClaudeAIService>();
                var isClaudeAvailable = await claudeService.IsServiceAvailableAsync();
                var claudeModels = await claudeService.GetAvailableModelsAsync();

                providers.Add(new AIProviderInfo
                {
                    Name = "Claude",
                    DisplayName = "Anthropic Claude",
                    IsAvailable = isClaudeAvailable,
                    Models = claudeModels,
                    Status = isClaudeAvailable ? "Healthy" : "Unhealthy"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Claude availability");
                providers.Add(new AIProviderInfo
                {
                    Name = "Claude",
                    DisplayName = "Anthropic Claude",
                    IsAvailable = false,
                    Status = "Not Configured"
                });
            }

            return new AvailableProvidersResponse
            {
                DefaultProvider = _config.DefaultProvider,
                Providers = providers
            };
        }

        public async Task<bool> IsProviderAvailableAsync(string providerName)
        {
            try
            {
                var provider = GetProvider(providerName);
                return await provider.IsServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking provider availability: {ProviderName}", providerName);
                return false;
            }
        }

        public string GetDefaultProvider()
        {
            return _config.DefaultProvider;
        }
    }
}