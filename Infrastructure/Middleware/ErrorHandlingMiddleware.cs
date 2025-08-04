using System.Net;
using System.Text.Json;
using AICodePortal.Backend.Models.Responses;

namespace AICodePortal.Backend.Infrastructure.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = exception switch
            {
                InvalidOperationException => new ApiResponse<object>
                {
                    Success = false,
                    Message = exception.Message,
                    Errors = new List<string> { exception.Message }
                },
                ArgumentException => new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid argument provided",
                    Errors = new List<string> { exception.Message }
                },
                UnauthorizedAccessException => new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unauthorized access",
                    Errors = new List<string> { "Access denied" }
                },
                _ => new ApiResponse<object>
                {
                    Success = false,
                    Message = "An internal server error occurred",
                    Errors = new List<string> { "Internal server error" }
                }
            };

            context.Response.StatusCode = exception switch
            {
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                _ => (int)HttpStatusCode.InternalServerError
            };

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}