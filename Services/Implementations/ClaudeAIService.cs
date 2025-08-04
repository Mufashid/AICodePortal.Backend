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
    public class ClaudeAIService : IAIService
    {
        private readonly IRepositoryService _repositoryService;
        private readonly HttpClient _httpClient;
        private readonly ClaudeConfiguration _config;
        private readonly ILogger<ClaudeAIService> _logger;

        public string ProviderName => "Claude";

        public ClaudeAIService(
            IRepositoryService repositoryService,
            HttpClient httpClient,
            IOptions<AIProviderConfiguration> config,
            ILogger<ClaudeAIService> logger)
        {
            _repositoryService = repositoryService;
            _httpClient = httpClient;
            _config = config.Value.Claude;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", _config.AnthropicVersion);
            _httpClient.Timeout = _config.RequestTimeout;
        }

        public async Task<CodeAnalysisResponse> AnalyzeCodeAsync(CodeAnalysisRequest request, Project project)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting Claude AI code analysis for project {ProjectName}", project.Name);

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

                // Call Claude API
                var aiResponse = await CallClaudeAPIAsync(context, request.Query, modelToUse);

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

                _logger.LogInformation("Claude AI code analysis completed for project {ProjectName} in {ProcessingTime}ms",
                    project.Name, stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing code with Claude AI for project {ProjectName}", project.Name);
                throw new InvalidOperationException($"Claude AI analysis failed: {ex.Message}", ex);
            }
        }

        public async Task<List<AIModelInfo>> GetAvailableModelsAsync()
        {
            return new List<AIModelInfo>
            {
                new AIModelInfo
                {
                    Name = "claude-3-opus-20240229",
                    DisplayName = "Claude 3 Opus",
                    Description = "Most capable model, best for complex analysis",
                    EstimatedCostPer1KTokens = 0.015m,
                    IsDefault = _config.Model == "claude-3-opus-20240229"
                },
                new AIModelInfo
                {
                    Name = "claude-3-sonnet-20240229",
                    DisplayName = "Claude 3 Sonnet",
                    Description = "Good balance of capability and cost",
                    EstimatedCostPer1KTokens = 0.003m,
                    IsDefault = _config.Model == "claude-3-sonnet-20240229"
                },
                new AIModelInfo
                {
                    Name = "claude-3-haiku-20240307",
                    DisplayName = "Claude 3 Haiku",
                    Description = "Fastest and most affordable option",
                    EstimatedCostPer1KTokens = 0.00025m,
                    IsDefault = _config.Model == "claude-3-haiku-20240307"
                }
            };
        }

        public async Task<decimal> EstimateCostAsync(string content, string model)
        {
            // Rough token estimation (1 token ≈ 0.75 words)
            var estimatedTokens = content.Split(' ').Length / 0.75;

            var costPer1KTokens = model switch
            {
                "claude-3-opus-20240229" => 0.015m,
                "claude-3-sonnet-20240229" => 0.003m,
                "claude-3-haiku-20240307" => 0.00025m,
                _ => 0.003m
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
                    max_tokens = 10,
                    messages = new[]
                    {
                        new { role = "user", content = "Test" }
                    }
                };

                var json = JsonSerializer.Serialize(testRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_config.BaseUrl}/messages", content);

                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.PaymentRequired;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude AI service availability check failed");
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
            foreach (var file in relevantFiles.Take(8))
            {
                try
                {
                    var content = await _repositoryService.ReadFileContentAsync(file);
                    var relativePath = Path.GetRelativePath(projectPath, file);

                    context.AppendLine($"### File: {relativePath}");
                    context.AppendLine("```");

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

            _logger.LogInformation("Built context with {FileCount} files for Claude AI analysis", fileContentsProcessed);
            return context.ToString();
        }

        private async Task<string> CallClaudeAPIAsync(string context, string query, string model)
        {
            try
            {
                var systemPrompt = @"You are an expert software architect and code analyst. Your role is to analyze code repositories and provide clear, detailed explanations about code structure, architecture, component relationships, interface implementations, database schemas, API endpoints, design patterns, and configuration details. Always provide practical, actionable insights and explain complex concepts in user-friendly terms.";

                var userMessage = $@"Please analyze the following code project and answer the user's question comprehensively.

{context}

User Question: {query}

Please provide a detailed analysis that includes:
1. Direct answer to the user's question
2. Relevant code explanations with specific examples
3. Architecture patterns and design decisions identified
4. Component mappings and relationships
5. Any recommendations or best practices observations

Format your response clearly with proper sections and explanations.";

                var requestBody = new
                {
                    model = model,
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = userMessage
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending request to Claude AI API with model {Model}", model);
                var response = await _httpClient.PostAsync($"{_config.BaseUrl}/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Claude AI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Claude AI API error: {response.StatusCode} - {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var aiResponse = responseObj
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "No response generated";

                _logger.LogInformation("Received response from Claude AI API");
                return aiResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Claude AI API");
                throw new InvalidOperationException($"Claude AI service call failed: {ex.Message}", ex);
            }
        }
    }
}
