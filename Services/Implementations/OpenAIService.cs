// ========================================================================================
// Services/Implementations/OpenAIService.cs - Complete Fixed Implementation
// ========================================================================================
using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Entities;
using AICodePortal.Backend.Core.Configurations;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using System.Diagnostics;

namespace AICodePortal.Backend.Services.Implementations
{
    public class OpenAIService : IAIService
    {
        private readonly IRepositoryService _repositoryService;
        private readonly HttpClient _httpClient;
        private readonly OpenAIConfiguration _config;
        private readonly ILogger<OpenAIService> _logger;

        // ✅ FIXED: ProviderName property implementation
        public string ProviderName { get; } = "OpenAI";

        public OpenAIService(
            IRepositoryService repositoryService,
            HttpClient httpClient,
            IOptions<AIProviderConfiguration> config,
            ILogger<OpenAIService> logger)
        {
            _repositoryService = repositoryService;
            _httpClient = httpClient;
            _config = config.Value.OpenAI; // ✅ FIXED: Access OpenAI config from AIProviderConfiguration
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            _httpClient.Timeout = _config.RequestTimeout;
        }

        public async Task<CodeAnalysisResponse> AnalyzeCodeAsync(CodeAnalysisRequest request, Project project)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting OpenAI code analysis for project {ProjectName} with query: {Query}",
                    project.Name, request.Query);

                // Use specific model if requested, otherwise use default
                var modelToUse = request.Model ?? _config.Model;

                // Clone/update repository
                var projectPath = await _repositoryService.CloneOrUpdateRepositoryAsync(
                    project.RepositoryUrl, project.RepositoryType.ToString(), project.Name);

                // Analyze project structure
                var projectStructure = await _repositoryService.AnalyzeProjectStructureAsync(projectPath);

                // Find relevant files based on query
                var relevantFiles = await _repositoryService.FindRelevantFilesAsync(request.Query, projectPath);

                // Build context for AI
                var context = await BuildContextForAIAsync(request.Query, relevantFiles, projectStructure, projectPath);

                // Call OpenAI API
                var aiResponse = await CallOpenAIAPIAsync(context, request.Query, modelToUse);

                stopwatch.Stop();

                var response = new CodeAnalysisResponse
                {
                    Response = aiResponse,
                    RelevantFiles = relevantFiles.Select(f => Path.GetRelativePath(projectPath, f)).ToList(),
                    ProjectStructure = JsonSerializer.Serialize(projectStructure, new JsonSerializerOptions { WriteIndented = true }),
                    ProcessingTime = stopwatch.Elapsed,
                    UsedProvider = ProviderName,
                    UsedModel = modelToUse,
                    EstimatedCost = await EstimateCostAsync(context + request.Query, modelToUse)
                };

                _logger.LogInformation("OpenAI code analysis completed for project {ProjectName} in {ProcessingTime}ms",
                    project.Name, stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing code with OpenAI for project {ProjectName}", project.Name);
                throw new InvalidOperationException($"OpenAI analysis failed: {ex.Message}", ex);
            }
        }

        // ✅ ADDED: GetAvailableModelsAsync method
        public async Task<List<AIModelInfo>> GetAvailableModelsAsync()
        {
            return new List<AIModelInfo>
            {
                new AIModelInfo
                {
                    Name = "gpt-4",
                    DisplayName = "GPT-4",
                    Description = "Most capable model, best for complex analysis",
                    EstimatedCostPer1KTokens = 0.03m,
                    IsDefault = _config.Model == "gpt-4"
                },
                new AIModelInfo
                {
                    Name = "gpt-4-turbo",
                    DisplayName = "GPT-4 Turbo",
                    Description = "Fast and capable, good balance of cost and quality",
                    EstimatedCostPer1KTokens = 0.01m,
                    IsDefault = _config.Model == "gpt-4-turbo"
                },
                new AIModelInfo
                {
                    Name = "gpt-3.5-turbo",
                    DisplayName = "GPT-3.5 Turbo",
                    Description = "Fast and affordable, good for simple analysis",
                    EstimatedCostPer1KTokens = 0.002m,
                    IsDefault = _config.Model == "gpt-3.5-turbo"
                }
            };
        }

        // ✅ ADDED: EstimateCostAsync method
        public async Task<decimal> EstimateCostAsync(string content, string model)
        {
            // Rough token estimation (1 token ≈ 0.75 words)
            var estimatedTokens = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / 0.75;

            var costPer1KTokens = model switch
            {
                "gpt-4" => 0.03m,
                "gpt-4-turbo" => 0.01m,
                "gpt-3.5-turbo" => 0.002m,
                _ => 0.01m
            };

            return (decimal)(estimatedTokens / 1000) * costPer1KTokens;
        }

        public async Task<string> GenerateProjectSummaryAsync(string projectPath)
        {
            try
            {
                var structure = await _repositoryService.AnalyzeProjectStructureAsync(projectPath);
                var summary = $"Project contains {structure.GetValueOrDefault("TotalFiles", 0)} files including " +
                             $"C# files, JavaScript files, HTML files, and configuration files.";
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate project summary for {ProjectPath}", projectPath);
                return "Unable to generate project summary.";
            }
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var testRequest = new
                {
                    model = _config.Model,
                    messages = new[] { new { role = "user", content = "Test" } },
                    max_tokens = 10
                };

                var json = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_config.BaseUrl}/chat/completions", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI service availability check failed");
                return false;
            }
        }

        private async Task<string> BuildContextForAIAsync(string query, List<string> relevantFiles,
            Dictionary<string, object> projectStructure, string projectPath)
        {
            var context = new StringBuilder();

            context.AppendLine("PROJECT ANALYSIS CONTEXT:");
            context.AppendLine($"User Query: {query}");
            context.AppendLine();

            context.AppendLine("PROJECT STRUCTURE:");
            context.AppendLine(JsonSerializer.Serialize(projectStructure, new JsonSerializerOptions { WriteIndented = true }));
            context.AppendLine();

            context.AppendLine("RELEVANT FILES CONTENT:");

            var fileContentsProcessed = 0;
            foreach (var file in relevantFiles.Take(8)) // Limit to prevent token overflow
            {
                try
                {
                    var content = await _repositoryService.ReadFileContentAsync(file);
                    var relativePath = Path.GetRelativePath(projectPath, file);

                    context.AppendLine($"### File: {relativePath}");
                    context.AppendLine("```");

                    // Limit file content to prevent token overflow
                    var limitedContent = content.Length > 3000 ? content.Substring(0, 3000) + "..." : content;
                    context.AppendLine(limitedContent);
                    context.AppendLine("```");
                    context.AppendLine();

                    fileContentsProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file content: {FilePath}", file);
                }
            }

            _logger.LogInformation("Built context with {FileCount} files for OpenAI analysis", fileContentsProcessed);
            return context.ToString();
        }

        private async Task<string> CallOpenAIAPIAsync(string context, string query, string model)
        {
            try
            {
                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = @"You are an expert software architect and code analyst. Your role is to analyze code repositories and provide clear, detailed explanations about:

1. Code structure and architecture
2. Component relationships and mappings
3. Interface implementations and connections
4. Database schemas and data flows
5. API endpoints and their purposes
6. Design patterns and best practices used
7. Configuration and deployment details

Always provide practical, actionable insights and explain complex concepts in user-friendly terms. When explaining mappings or interfaces, be specific about how components connect and interact."
                        },
                        new
                        {
                            role = "user",
                            content = $@"Please analyze the following code project and answer the user's question comprehensively.

{context}

User Question: {query}

Please provide a detailed analysis that includes:
1. Direct answer to the user's question
2. Relevant code explanations with specific examples
3. Architecture patterns and design decisions identified
4. Component mappings and relationships
5. Any recommendations or best practices observations

Format your response clearly with proper sections and explanations."
                        }
                    },
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending request to OpenAI API with model {Model}", model);
                var response = await _httpClient.PostAsync($"{_config.BaseUrl}/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var aiResponse = responseObj
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "No response generated";

                _logger.LogInformation("Received response from OpenAI API");
                return aiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call OpenAI API");
                throw new InvalidOperationException($"OpenAI service call failed: {ex.Message}", ex);
            }
        }
    }
}