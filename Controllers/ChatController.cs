using Microsoft.AspNetCore.Mvc;
using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Responses;
using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IAIProviderFactory _aiProviderFactory;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            IAIProviderFactory aiProviderFactory,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _aiProviderFactory = aiProviderFactory;
            _logger = logger;
        }

        [HttpGet("providers")]
        public async Task<ActionResult<ApiResponse<AvailableProvidersResponse>>> GetAvailableProviders()
        {
            try
            {
                var providers = await _aiProviderFactory.GetAvailableProvidersAsync();
                return Ok(ApiResponse<AvailableProvidersResponse>.SuccessResponse(providers));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available AI providers");
                return StatusCode(500, ApiResponse<AvailableProvidersResponse>.ErrorResponse("Failed to retrieve AI providers"));
            }
        }

        [HttpPost("analyze")]
        public async Task<ActionResult<ApiResponse<CodeAnalysisResponse>>> AnalyzeCode([FromBody] CodeAnalysisRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<CodeAnalysisResponse>.ErrorResponse("Validation failed", errors));
                }

                // Validate AI provider if specified
                if (!string.IsNullOrEmpty(request.AIProvider))
                {
                    var isAvailable = await _aiProviderFactory.IsProviderAvailableAsync(request.AIProvider);
                    if (!isAvailable)
                    {
                        return BadRequest(ApiResponse<CodeAnalysisResponse>.ErrorResponse(
                            $"AI provider '{request.AIProvider}' is not available or not configured"));
                    }
                }

                var response = await _chatService.ProcessChatMessageAsync(request);
                return Ok(ApiResponse<CodeAnalysisResponse>.SuccessResponse(response, "Code analysis completed"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<CodeAnalysisResponse>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing code for project: {ProjectName} with provider: {Provider}",
                    request.ProjectName, request.AIProvider ?? "default");
                return StatusCode(500, ApiResponse<CodeAnalysisResponse>.ErrorResponse("Code analysis failed"));
            }
        }

        [HttpGet("sessions/{projectId}")]
        public async Task<ActionResult<ApiResponse<IEnumerable<ChatSessionDto>>>> GetChatSessions(int projectId)
        {
            try
            {
                var sessions = await _chatService.GetChatSessionsAsync(projectId);
                return Ok(ApiResponse<IEnumerable<ChatSessionDto>>.SuccessResponse(sessions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat sessions for project {ProjectId}", projectId);
                return StatusCode(500, ApiResponse<IEnumerable<ChatSessionDto>>.ErrorResponse("Failed to retrieve chat sessions"));
            }
        }

        [HttpGet("sessions/{projectId}/{sessionId}")]
        public async Task<ActionResult<ApiResponse<ChatSessionDto>>> GetChatSession(int projectId, string sessionId)
        {
            try
            {
                var session = await _chatService.GetChatSessionAsync(projectId, sessionId);
                if (session == null)
                {
                    return NotFound(ApiResponse<ChatSessionDto>.ErrorResponse("Chat session not found"));
                }

                return Ok(ApiResponse<ChatSessionDto>.SuccessResponse(session));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat session {SessionId} for project {ProjectId}", sessionId, projectId);
                return StatusCode(500, ApiResponse<ChatSessionDto>.ErrorResponse("Failed to retrieve chat session"));
            }
        }

        [HttpPost("sessions")]
        public async Task<ActionResult<ApiResponse<ChatSessionDto>>> CreateChatSession([FromBody] CreateChatSessionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ChatSessionDto>.ErrorResponse("Validation failed", errors));
                }

                var session = await _chatService.CreateChatSessionAsync(request.ProjectId, request.SessionId, request.Title);
                return CreatedAtAction(nameof(GetChatSession),
                    new { projectId = request.ProjectId, sessionId = request.SessionId },
                    ApiResponse<ChatSessionDto>.SuccessResponse(session, "Chat session created successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat session for project {ProjectId}", request.ProjectId);
                return StatusCode(500, ApiResponse<ChatSessionDto>.ErrorResponse("Failed to create chat session"));
            }
        }
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