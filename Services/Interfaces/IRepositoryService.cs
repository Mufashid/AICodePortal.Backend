namespace AICodePortal.Backend.Services.Interfaces
{
    public interface IRepositoryService
    {
        Task<string> CloneOrUpdateRepositoryAsync(string repositoryUrl, string repositoryType, string projectName);
        Task<List<string>> GetProjectFilesAsync(string projectPath, string fileExtension = "");
        Task<string> ReadFileContentAsync(string filePath);
        Task<Dictionary<string, object>> AnalyzeProjectStructureAsync(string projectPath);
        Task<List<string>> FindRelevantFilesAsync(string query, string projectPath);
        Task<bool> ValidateRepositoryAsync(string repositoryUrl, string repositoryType);

        // Add these new methods
        Task<bool> CleanupRepositoryAsync(string projectName);
        Task<string> ForceCloneRepositoryAsync(string repositoryUrl, string repositoryType, string projectName);
    }
}