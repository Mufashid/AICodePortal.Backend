using AICodePortal.Backend.Models.DTOs;

namespace AICodePortal.Backend.Services.Interfaces
{
    public interface IAIProviderFactory
    {
        IAIService GetProvider(string? providerName = null);
        Task<AvailableProvidersResponse> GetAvailableProvidersAsync();
        Task<bool> IsProviderAvailableAsync(string providerName);
        string GetDefaultProvider();
    }
}