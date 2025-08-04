using AICodePortal.Backend.Models.DTOs;

namespace AICodePortal.Backend.Services.Interfaces
{
    public interface IChatService
    {
        Task<CodeAnalysisResponse> ProcessChatMessageAsync(CodeAnalysisRequest request);
        Task<IEnumerable<ChatSessionDto>> GetChatSessionsAsync(int projectId);
        Task<ChatSessionDto?> GetChatSessionAsync(int projectId, string sessionId);
        Task<ChatSessionDto> CreateChatSessionAsync(int projectId, string sessionId, string title = "New Chat");
    }
}