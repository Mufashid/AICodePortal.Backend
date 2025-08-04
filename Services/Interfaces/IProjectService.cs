using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Entities;

namespace AICodePortal.Backend.Services.Interfaces
{
    public interface IProjectService
    {
        Task<IEnumerable<ProjectDto>> GetAllProjectsAsync();
        Task<ProjectDto?> GetProjectByIdAsync(int id);
        Task<ProjectDto?> GetProjectByNameAsync(string name);
        Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto);
        Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto updateProjectDto);
        Task<bool> DeleteProjectAsync(int id);
        Task<bool> SyncProjectAsync(int id);
    }
}