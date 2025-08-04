// ========================================================================================
// Complete RepositoryService Implementation - Replace your existing RepositoryService
// ========================================================================================
using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Core.Configurations;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace AICodePortal.Backend.Services.Implementations
{
    public class RepositoryService : IRepositoryService
    {
        private readonly string _baseRepositoryPath;
        private readonly ILogger<RepositoryService> _logger;
        private readonly RepositoryConfiguration _config;

        public RepositoryService(ILogger<RepositoryService> logger, IOptions<RepositoryConfiguration> config)
        {
            _logger = logger;
            _config = config.Value;
            _baseRepositoryPath = _config.BasePath ?? Path.Combine(Directory.GetCurrentDirectory(), "Storage", "Repositories");
            Directory.CreateDirectory(_baseRepositoryPath);
        }

        public async Task<string> CloneOrUpdateRepositoryAsync(string repositoryUrl, string repositoryType, string projectName)
        {
            var sanitizedProjectName = SanitizeProjectName(projectName);
            var projectPath = Path.Combine(_baseRepositoryPath, sanitizedProjectName);

            try
            {
                if (Directory.Exists(projectPath))
                {
                    // Check if it's a valid repository
                    if (IsValidRepository(projectPath, repositoryType))
                    {
                        _logger.LogInformation("Updating existing repository at {ProjectPath}", projectPath);
                        await UpdateRepositoryAsync(projectPath, repositoryType);
                    }
                    else
                    {
                        _logger.LogInformation("Directory exists but is not a valid {RepositoryType} repository, re-cloning", repositoryType);
                        await CloneRepositoryAsync(repositoryUrl, repositoryType, projectPath);
                    }
                }
                else
                {
                    _logger.LogInformation("Cloning new repository to {ProjectPath}", projectPath);
                    await CloneRepositoryAsync(repositoryUrl, repositoryType, projectPath);
                }

                return projectPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clone/update repository {RepositoryUrl}", repositoryUrl);

                // If we have an existing directory with some files, we can still proceed
                if (Directory.Exists(projectPath) && Directory.GetFiles(projectPath, "*", SearchOption.AllDirectories).Length > 0)
                {
                    _logger.LogWarning("Using existing files in {ProjectPath} despite clone/update failure", projectPath);
                    return projectPath;
                }

                throw new InvalidOperationException($"Failed to clone/update repository and no existing files available: {ex.Message}", ex);
            }
        }

        public async Task<List<string>> GetProjectFilesAsync(string projectPath, string fileExtension = "")
        {
            var files = new List<string>();

            if (!Directory.Exists(projectPath))
            {
                _logger.LogWarning("Project path does not exist: {ProjectPath}", projectPath);
                return files;
            }

            var searchPattern = string.IsNullOrEmpty(fileExtension) ? "*.*" : $"*.{fileExtension}";
            var allFiles = Directory.GetFiles(projectPath, searchPattern, SearchOption.AllDirectories);

            // Filter out common non-code directories and files
            var excludeDirs = new[] { "bin", "obj", "node_modules", ".git", ".svn", "packages", "dist", "build" };
            var excludeExtensions = new[] { ".exe", ".dll", ".pdb", ".cache", ".tmp" };

            files.AddRange(allFiles.Where(file =>
                !excludeDirs.Any(dir => file.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar)) &&
                !excludeExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) &&
                new FileInfo(file).Length <= _config.MaxFileSize));

            _logger.LogInformation("Found {FileCount} files in project {ProjectPath}", files.Count, projectPath);
            return files;
        }

        public async Task<string> ReadFileContentAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File does not exist: {FilePath}", filePath);
                return string.Empty;
            }

            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read file: {FilePath}", filePath);
                return string.Empty;
            }
        }

        public async Task<Dictionary<string, object>> AnalyzeProjectStructureAsync(string projectPath)
        {
            var structure = new Dictionary<string, object>();

            if (!Directory.Exists(projectPath))
            {
                _logger.LogWarning("Project path does not exist: {ProjectPath}", projectPath);
                return structure;
            }

            try
            {
                // Get files by type
                var csharpFiles = await GetProjectFilesAsync(projectPath, "cs");
                var jsFiles = await GetProjectFilesAsync(projectPath, "js");
                var htmlFiles = await GetProjectFilesAsync(projectPath, "html");
                var cssFiles = await GetProjectFilesAsync(projectPath, "css");
                var configFiles = Directory.GetFiles(projectPath, "*.config", SearchOption.AllDirectories)
                                           .Concat(Directory.GetFiles(projectPath, "*.json", SearchOption.AllDirectories))
                                           .Take(20).ToList();

                structure["ProjectPath"] = projectPath;
                structure["CSharpFiles"] = csharpFiles.Take(50).Select(f => Path.GetRelativePath(projectPath, f)).ToList();
                structure["JavaScriptFiles"] = jsFiles.Take(50).Select(f => Path.GetRelativePath(projectPath, f)).ToList();
                structure["HtmlFiles"] = htmlFiles.Take(50).Select(f => Path.GetRelativePath(projectPath, f)).ToList();
                structure["CssFiles"] = cssFiles.Take(50).Select(f => Path.GetRelativePath(projectPath, f)).ToList();
                structure["ConfigFiles"] = configFiles.Select(f => Path.GetRelativePath(projectPath, f)).ToList();
                structure["TotalFiles"] = csharpFiles.Count + jsFiles.Count + htmlFiles.Count + cssFiles.Count;
                structure["LastAnalyzed"] = DateTime.UtcNow;

                _logger.LogInformation("Analyzed project structure for {ProjectPath}", projectPath);
                return structure;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze project structure: {ProjectPath}", projectPath);
                throw;
            }
        }

        public async Task<List<string>> FindRelevantFilesAsync(string query, string projectPath)
        {
            var relevantFiles = new List<string>();
            var allFiles = await GetProjectFilesAsync(projectPath);

            // Extract keywords from query
            var keywords = ExtractKeywords(query);

            // Score files based on relevance
            var fileScores = new Dictionary<string, int>();

            foreach (var file in allFiles.Take(200)) // Limit for performance
            {
                var score = 0;
                var fileName = Path.GetFileName(file);
                var content = await ReadFileContentAsync(file);

                // Score based on filename matches
                foreach (var keyword in keywords)
                {
                    if (fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        score += 10;

                    if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        score += content.Split(keyword, StringSplitOptions.RemoveEmptyEntries).Length - 1;
                }

                if (score > 0)
                    fileScores[file] = score;
            }

            // Return top 15 most relevant files
            relevantFiles = fileScores
                .OrderByDescending(kvp => kvp.Value)
                .Take(15)
                .Select(kvp => kvp.Key)
                .ToList();

            _logger.LogInformation("Found {RelevantFileCount} relevant files for query: {Query}", relevantFiles.Count, query);
            return relevantFiles;
        }

        public async Task<bool> ValidateRepositoryAsync(string repositoryUrl, string repositoryType)
        {
            try
            {
                if (repositoryType.ToUpper() == "GIT")
                {
                    var result = await ExecuteCommandAsync($"git ls-remote {repositoryUrl}", _baseRepositoryPath, TimeSpan.FromSeconds(30));
                    return result.Success;
                }
                else if (repositoryType.ToUpper() == "SVN")
                {
                    var result = await ExecuteCommandAsync($"svn info {repositoryUrl}", _baseRepositoryPath, TimeSpan.FromSeconds(30));
                    return result.Success;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate repository: {RepositoryUrl}", repositoryUrl);
                return false;
            }
        }

        // NEW: Cleanup repository method
        public async Task<bool> CleanupRepositoryAsync(string projectName)
        {
            try
            {
                var sanitizedProjectName = SanitizeProjectName(projectName);
                var projectPath = Path.Combine(_baseRepositoryPath, sanitizedProjectName);

                if (Directory.Exists(projectPath))
                {
                    _logger.LogInformation("Cleaning up repository directory: {ProjectPath}", projectPath);

                    // Try to delete the directory multiple times if needed
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            Directory.Delete(projectPath, true);
                            await Task.Delay(500); // Wait for filesystem

                            if (!Directory.Exists(projectPath))
                            {
                                _logger.LogInformation("Successfully cleaned up repository directory: {ProjectPath}", projectPath);
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Attempt {Attempt} to delete directory {ProjectPath} failed", i + 1, projectPath);
                            await Task.Delay(1000);
                        }
                    }
                }

                return !Directory.Exists(projectPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup repository for project {ProjectName}", projectName);
                return false;
            }
        }

        // NEW: Force clone repository method
        public async Task<string> ForceCloneRepositoryAsync(string repositoryUrl, string repositoryType, string projectName)
        {
            try
            {
                // First, cleanup any existing directory
                await CleanupRepositoryAsync(projectName);

                // Then clone fresh
                var sanitizedProjectName = SanitizeProjectName(projectName);
                var projectPath = Path.Combine(_baseRepositoryPath, sanitizedProjectName);

                await CloneRepositoryAsync(repositoryUrl, repositoryType, projectPath);

                return projectPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to force clone repository {RepositoryUrl}", repositoryUrl);
                throw;
            }
        }

        // Private helper methods
        private async Task CloneRepositoryAsync(string repositoryUrl, string repositoryType, string targetPath)
        {
            // If directory exists, delete it first
            if (Directory.Exists(targetPath))
            {
                _logger.LogInformation("Directory {TargetPath} already exists, deleting it", targetPath);
                try
                {
                    await SafeDeleteDirectory(targetPath);
                    // Wait a bit for the filesystem to catch up
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete existing directory {TargetPath}, trying to continue", targetPath);
                }
            }

            var command = repositoryType.ToUpper() switch
            {
                "GIT" => $"git clone \"{repositoryUrl}\" \"{targetPath}\"",
                "SVN" => $"svn checkout \"{repositoryUrl}\" \"{targetPath}\"",
                _ => throw new ArgumentException($"Unsupported repository type: {repositoryType}")
            };

            var result = await ExecuteCommandAsync(command, _baseRepositoryPath, TimeSpan.FromMinutes(10));
            if (!result.Success)
            {
                // If clone still fails, try cleaning up and retrying once
                if (Directory.Exists(targetPath))
                {
                    try
                    {
                        await SafeDeleteDirectory(targetPath);
                        await Task.Delay(1000);

                        var retryResult = await ExecuteCommandAsync(command, _baseRepositoryPath, TimeSpan.FromMinutes(10));
                        if (!retryResult.Success)
                        {
                            throw new InvalidOperationException($"Clone command failed after retry: {retryResult.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Clone command failed and cleanup failed: {result.Error}. Cleanup error: {ex.Message}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Clone command failed: {result.Error}");
                }
            }
        }

        private async Task UpdateRepositoryAsync(string projectPath, string repositoryType)
        {
            if (!Directory.Exists(projectPath))
            {
                _logger.LogWarning("Project path {ProjectPath} does not exist for update", projectPath);
                throw new InvalidOperationException($"Project path does not exist: {projectPath}");
            }

            var command = repositoryType.ToUpper() switch
            {
                "GIT" => "git pull origin",
                "SVN" => "svn update",
                _ => throw new ArgumentException($"Unsupported repository type: {repositoryType}")
            };

            var result = await ExecuteCommandAsync(command, projectPath, TimeSpan.FromMinutes(5));
            if (!result.Success)
            {
                _logger.LogWarning("Update command failed for {ProjectPath}: {Error}", projectPath, result.Error);
                // For update failures, we can still proceed with existing files
                _logger.LogInformation("Continuing with existing repository files at {ProjectPath}", projectPath);
            }
        }

        private bool IsValidRepository(string projectPath, string repositoryType)
        {
            try
            {
                return repositoryType.ToUpper() switch
                {
                    "GIT" => Directory.Exists(Path.Combine(projectPath, ".git")),
                    "SVN" => Directory.Exists(Path.Combine(projectPath, ".svn")),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        private async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string command, string workingDirectory, TimeSpan timeout)
        {
            try
            {
                var processInfo = new ProcessStartInfo("cmd", "/c " + command)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait for process to exit with timeout
                var processExited = process.WaitForExit((int)timeout.TotalMilliseconds);

                if (!processExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Ignore kill errors
                    }
                    return (false, string.Empty, "Process timed out");
                }

                var output = await outputTask;
                var error = await errorTask;

                var success = process.ExitCode == 0;

                _logger.LogInformation("Command executed: {Command}, Success: {Success}", command, success);

                return (success, output, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command: {Command}", command);
                return (false, string.Empty, ex.Message);
            }
        }

        private static List<string> ExtractKeywords(string query)
        {
            var commonWords = new[] { "the", "is", "at", "which", "on", "how", "what", "where", "when", "why", "and", "or", "but", "in", "with", "for" };
            return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                       .Where(word => word.Length > 2 && !commonWords.Contains(word.ToLower()))
                       .Select(word => word.Trim('?', '.', ',', '!', ';', ':'))
                       .ToList();
        }

        private static string SanitizeProjectName(string projectName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", projectName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        // Helper method to handle file locking issues on Windows
        private static async Task SafeDeleteDirectory(string path, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        // Remove read-only attributes that might prevent deletion
                        var dirInfo = new DirectoryInfo(path);
                        SetAttributesNormal(dirInfo);

                        Directory.Delete(path, true);
                    }

                    if (!Directory.Exists(path))
                        return; // Success
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    // Wait and retry
                    await Task.Delay(1000 * (i + 1));
                }
            }
        }

        private static void SetAttributesNormal(DirectoryInfo dir)
        {
            try
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    SetAttributesNormal(subDir);
                }

                foreach (var file in dir.GetFiles())
                {
                    file.Attributes = FileAttributes.Normal;
                }

                dir.Attributes = FileAttributes.Normal;
            }
            catch
            {
                // Ignore errors in attribute setting
            }
        }
    }
}