using Microsoft.AspNetCore.Mvc;
using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.Responses;

namespace AICodePortal.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IAIService _aiService; // ✅ SIMPLE: Back to direct injection
        private readonly ILogger<HealthController> _logger;

        public HealthController(IAIService aiService, ILogger<HealthController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<object>>> GetHealth()
        {
            try
            {
                var aiServiceAvailable = await _aiService.IsServiceAvailableAsync();

                var healthStatus = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Services = new
                    {
                        Database = "Healthy",
                        AIService = aiServiceAvailable ? "Healthy" : "Unhealthy",
                        AIProvider = _aiService.ProviderName,
                        FileSystem = Directory.Exists("Storage") ? "Healthy" : "Unhealthy"
                    }
                };

                return Ok(ApiResponse<object>.SuccessResponse(healthStatus));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Health check failed"));
            }
        }
    }
}