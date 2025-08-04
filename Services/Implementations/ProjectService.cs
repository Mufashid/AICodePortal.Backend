using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Entities;
using AICodePortal.Backend.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace AICodePortal.Backend.Services.Implementations
{
    public class ProjectService : IProjectService
    {
        private readonly AIPortalDbContext _context;
        private readonly IRepositoryService _repositoryService;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            AIPortalDbContext context,
            IRepositoryService repositoryService,
            ILogger<ProjectService> logger)
        {
            _context = context;
            _repositoryService = repositoryService;
            _logger = logger;
        }

        public async Task<IEnumerable<ProjectDto>> GetAllProjectsAsync()
        {
            try
            {
                var projects = await _context.Projects
                    .Where(p => !p.IsDeleted)
                    .Include(p => p.ChatSessions)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                var projectDtos = projects.Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    ClientName = p.ClientName,
                    RepositoryUrl = p.RepositoryUrl,
                    RepositoryType = p.RepositoryType.ToString(),
                    Description = p.Description,
                    Status = p.Status.ToString(),
                    CreatedAt = p.CreatedAt,
                    LastSyncedAt = p.LastSyncedAt,
                    ChatSessionsCount = p.ChatSessions.Count
                }).ToList();

                _logger.LogInformation("Retrieved {ProjectCount} projects", projectDtos.Count);
                return projectDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                throw;
            }
        }

        public async Task<ProjectDto?> GetProjectByIdAsync(int id)
        {
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id && !p.IsDeleted)
                    .Include(p => p.ChatSessions)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found", id);
                    return null;
                }

                return new ProjectDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    ClientName = project.ClientName,
                    RepositoryUrl = project.RepositoryUrl,
                    RepositoryType = project.RepositoryType.ToString(),
                    Description = project.Description,
                    Status = project.Status.ToString(),
                    CreatedAt = project.CreatedAt,
                    LastSyncedAt = project.LastSyncedAt,
                    ChatSessionsCount = project.ChatSessions.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project with ID {ProjectId}", id);
                throw;
            }
        }

        public async Task<ProjectDto?> GetProjectByNameAsync(string name)
        {
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Name == name && !p.IsDeleted)
                    .Include(p => p.ChatSessions)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning("Project with name {ProjectName} not found", name);
                    return null;
                }

                return new ProjectDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    ClientName = project.ClientName,
                    RepositoryUrl = project.RepositoryUrl,
                    RepositoryType = project.RepositoryType.ToString(),
                    Description = project.Description,
                    Status = project.Status.ToString(),
                    CreatedAt = project.CreatedAt,
                    LastSyncedAt = project.LastSyncedAt,
                    ChatSessionsCount = project.ChatSessions.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project with name {ProjectName}", name);
                throw;
            }
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createProjectDto)
        {
            try
            {
                // Validate repository
                var repositoryType = Enum.Parse<RepositoryType>(createProjectDto.RepositoryType, true);
                var isValidRepository = await _repositoryService.ValidateRepositoryAsync(
                    createProjectDto.RepositoryUrl, createProjectDto.RepositoryType);

                if (!isValidRepository)
                {
                    throw new InvalidOperationException("Invalid or inaccessible repository URL");
                }

                // Check if project with same name already exists
                var existingProject = await _context.Projects
                    .AnyAsync(p => p.Name == createProjectDto.Name && !p.IsDeleted);

                if (existingProject)
                {
                    throw new InvalidOperationException($"Project with name '{createProjectDto.Name}' already exists");
                }

                var project = new Project
                {
                    Name = createProjectDto.Name,
                    ClientName = createProjectDto.ClientName,
                    RepositoryUrl = createProjectDto.RepositoryUrl,
                    RepositoryType = repositoryType,
                    Description = createProjectDto.Description,
                    Status = ProjectStatus.Active
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                // Try to clone repository initially
                try
                {
                    var projectPath = await _repositoryService.CloneOrUpdateRepositoryAsync(
                        project.RepositoryUrl, project.RepositoryType.ToString(), project.Name);

                    project.LocalPath = projectPath;
                    project.LastSyncedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initially clone repository for project {ProjectName}", project.Name);
                    project.Status = ProjectStatus.Error;
                    await _context.SaveChangesAsync();
                }

                var projectDto = new ProjectDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    ClientName = project.ClientName,
                    RepositoryUrl = project.RepositoryUrl,
                    RepositoryType = project.RepositoryType.ToString(),
                    Description = project.Description,
                    Status = project.Status.ToString(),
                    CreatedAt = project.CreatedAt,
                    LastSyncedAt = project.LastSyncedAt,
                    ChatSessionsCount = 0
                };

                _logger.LogInformation("Created new project: {ProjectName}", project.Name);
                return projectDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project: {ProjectName}", createProjectDto.Name);
                throw;
            }
        }

        public async Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto updateProjectDto)
        {
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id && !p.IsDeleted)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found for update", id);
                    return null;
                }

                // Check if new name conflicts with existing project
                if (project.Name != updateProjectDto.Name)
                {
                    var nameExists = await _context.Projects
                        .AnyAsync(p => p.Name == updateProjectDto.Name && p.Id != id && !p.IsDeleted);

                    if (nameExists)
                    {
                        throw new InvalidOperationException($"Project with name '{updateProjectDto.Name}' already exists");
                    }
                }

                project.Name = updateProjectDto.Name;
                project.ClientName = updateProjectDto.ClientName;
                project.Description = updateProjectDto.Description;

                await _context.SaveChangesAsync();

                var projectDto = new ProjectDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    ClientName = project.ClientName,
                    RepositoryUrl = project.RepositoryUrl,
                    RepositoryType = project.RepositoryType.ToString(),
                    Description = project.Description,
                    Status = project.Status.ToString(),
                    CreatedAt = project.CreatedAt,
                    LastSyncedAt = project.LastSyncedAt,
                    ChatSessionsCount = await _context.ChatSessions.CountAsync(cs => cs.ProjectId == project.Id)
                };

                _logger.LogInformation("Updated project: {ProjectName}", project.Name);
                return projectDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project with ID {ProjectId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteProjectAsync(int id)
        {
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id && !p.IsDeleted)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found for deletion", id);
                    return false;
                }

                // Soft delete
                project.IsDeleted = true;
                await _context.SaveChangesAsync();

                // Clean up local repository files (optional)
                if (!string.IsNullOrEmpty(project.LocalPath) && Directory.Exists(project.LocalPath))
                {
                    try
                    {
                        Directory.Delete(project.LocalPath, true);
                        _logger.LogInformation("Deleted local repository files for project: {ProjectName}", project.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete local repository files for project: {ProjectName}", project.Name);
                    }
                }

                _logger.LogInformation("Deleted project: {ProjectName}", project.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project with ID {ProjectId}", id);
                throw;
            }
        }

        public async Task<bool> SyncProjectAsync(int id)
        {
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id && !p.IsDeleted)
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning("Project with ID {ProjectId} not found for sync", id);
                    return false;
                }

                try
                {
                    var projectPath = await _repositoryService.CloneOrUpdateRepositoryAsync(
                        project.RepositoryUrl, project.RepositoryType.ToString(), project.Name);

                    project.LocalPath = projectPath;
                    project.LastSyncedAt = DateTime.UtcNow;
                    project.Status = ProjectStatus.Active;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully synced project: {ProjectName}", project.Name);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync project: {ProjectName}", project.Name);
                    project.Status = ProjectStatus.Error;
                    await _context.SaveChangesAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing project with ID {ProjectId}", id);
                throw;
            }
        }
    }
}
