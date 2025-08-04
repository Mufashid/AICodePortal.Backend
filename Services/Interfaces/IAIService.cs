using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Entities;

namespace AICodePortal.Backend.Services.Interfaces
{
    public interface IAIService
    {
        string ProviderName { get; }
        Task<CodeAnalysisResponse> AnalyzeCodeAsync(CodeAnalysisRequest request, Project project);
        Task<string> GenerateProjectSummaryAsync(string projectPath);
        Task<bool> IsServiceAvailableAsync();
        Task<List<AIModelInfo>> GetAvailableModelsAsync();
        Task<decimal> EstimateCostAsync(string content, string model);
    }
}