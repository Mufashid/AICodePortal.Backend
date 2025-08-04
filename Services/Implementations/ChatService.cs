using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Entities;
using AICodePortal.Backend.Data.Context;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AICodePortal.Backend.Services.Implementations
{
    public class ChatService : IChatService
    {
        private readonly AIPortalDbContext _context;
        private readonly IProjectService _projectService;
        private readonly IAIService _aiService; // ✅ SIMPLE: Back to direct injection
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            AIPortalDbContext context,
            IProjectService projectService,
            IAIService aiService, // ✅ SIMPLE: Direct injection
            ILogger<ChatService> logger)
        {
            _context = context;
            _projectService = projectService;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<CodeAnalysisResponse> ProcessChatMessageAsync(CodeAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("Processing chat message for project: {ProjectName}", request.ProjectName);

                var project = await _context.Projects
                    .Where(p => p.Name == request.ProjectName && !p.IsDeleted)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    throw new InvalidOperationException($"Project '{request.ProjectName}' not found");
                }

                // Get or create chat session
                var session = await GetOrCreateChatSessionAsync(project.Id, request.SessionId);

                // Process with AI service (will use Claude)
                var response = await _aiService.AnalyzeCodeAsync(request, project);

                // Save chat message with provider info
                var chatMessage = new ChatMessage
                {
                    ChatSessionId = session.Id,
                    UserMessage = request.Query,
                    AiResponse = response.Response,
                    RelevantFiles = JsonSerializer.Serialize(response.RelevantFiles),
                    ProjectStructure = response.ProjectStructure,
                    ProcessingTime = response.ProcessingTime
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Chat message processed with {Provider} and saved for session: {SessionId}",
                    response.UsedProvider, request.SessionId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message for project: {ProjectName}", request.ProjectName);
                throw;
            }
        }

        // ... rest of the methods remain the same ...

        public async Task<IEnumerable<ChatSessionDto>> GetChatSessionsAsync(int projectId)
        {
            try
            {
                var sessions = await _context.ChatSessions
                    .Where(s => s.ProjectId == projectId)
                    .Include(s => s.Messages)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();

                var sessionDtos = sessions.Select(s => new ChatSessionDto
                {
                    Id = s.Id,
                    SessionId = s.SessionId,
                    Title = s.Title,
                    CreatedAt = s.CreatedAt,
                    MessageCount = s.Messages.Count,
                    Messages = s.Messages.OrderBy(m => m.CreatedAt).Select(m => new ChatMessageDto
                    {
                        Id = m.Id,
                        UserMessage = m.UserMessage,
                        AiResponse = m.AiResponse,
                        RelevantFiles = string.IsNullOrEmpty(m.RelevantFiles) ?
                            new List<string>() :
                            JsonSerializer.Deserialize<List<string>>(m.RelevantFiles) ?? new List<string>(),
                        Timestamp = m.CreatedAt,
                        ProcessingTime = m.ProcessingTime
                    }).ToList()
                }).ToList();

                return sessionDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat sessions for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<ChatSessionDto?> GetChatSessionAsync(int projectId, string sessionId)
        {
            try
            {
                var session = await _context.ChatSessions
                    .Where(s => s.ProjectId == projectId && s.SessionId == sessionId)
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return null;
                }

                return new ChatSessionDto
                {
                    Id = session.Id,
                    SessionId = session.SessionId,
                    Title = session.Title,
                    CreatedAt = session.CreatedAt,
                    MessageCount = session.Messages.Count,
                    Messages = session.Messages.OrderBy(m => m.CreatedAt).Select(m => new ChatMessageDto
                    {
                        Id = m.Id,
                        UserMessage = m.UserMessage,
                        AiResponse = m.AiResponse,
                        RelevantFiles = string.IsNullOrEmpty(m.RelevantFiles) ?
                            new List<string>() :
                            JsonSerializer.Deserialize<List<string>>(m.RelevantFiles) ?? new List<string>(),
                        Timestamp = m.CreatedAt,
                        ProcessingTime = m.ProcessingTime
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat session {SessionId} for project {ProjectId}", sessionId, projectId);
                throw;
            }
        }

        public async Task<ChatSessionDto> CreateChatSessionAsync(int projectId, string sessionId, string title = "New Chat")
        {
            try
            {
                var session = new ChatSession
                {
                    ProjectId = projectId,
                    SessionId = sessionId,
                    Title = title
                };

                _context.ChatSessions.Add(session);
                await _context.SaveChangesAsync();

                return new ChatSessionDto
                {
                    Id = session.Id,
                    SessionId = session.SessionId,
                    Title = session.Title,
                    CreatedAt = session.CreatedAt,
                    MessageCount = 0,
                    Messages = new List<ChatMessageDto>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat session {SessionId} for project {ProjectId}", sessionId, projectId);
                throw;
            }
        }

        private async Task<ChatSession> GetOrCreateChatSessionAsync(int projectId, string sessionId)
        {
            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SessionId == sessionId);

            if (session == null)
            {
                session = new ChatSession
                {
                    ProjectId = projectId,
                    SessionId = sessionId ?? Guid.NewGuid().ToString(),
                    Title = "New Chat"
                };

                _context.ChatSessions.Add(session);
                await _context.SaveChangesAsync();
            }

            return session;
        }
    }
}