using Microsoft.AspNetCore.Mvc;
using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Models.DTOs;
using AICodePortal.Backend.Models.Responses;
using System.ComponentModel.DataAnnotations;

namespace AICodePortal.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<ProjectDto>>>> GetProjects()
        {
            try
            {
                var projects = await _projectService.GetAllProjectsAsync();
                return Ok(ApiResponse<IEnumerable<ProjectDto>>.SuccessResponse(projects));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                return StatusCode(500, ApiResponse<IEnumerable<ProjectDto>>.ErrorResponse("Failed to retrieve projects"));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(int id)
        {
            try
            {
                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                {
                    return NotFound(ApiResponse<ProjectDto>.ErrorResponse("Project not found"));
                }

                return Ok(ApiResponse<ProjectDto>.SuccessResponse(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project with ID {ProjectId}", id);
                return StatusCode(500, ApiResponse<ProjectDto>.ErrorResponse("Failed to retrieve project"));
            }
        }

        [HttpGet("by-name/{name}")]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProjectByName(string name)
        {
            try
            {
                var project = await _projectService.GetProjectByNameAsync(name);
                if (project == null)
                {
                    return NotFound(ApiResponse<ProjectDto>.ErrorResponse("Project not found"));
                }

                return Ok(ApiResponse<ProjectDto>.SuccessResponse(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project with name {ProjectName}", name);
                return StatusCode(500, ApiResponse<ProjectDto>.ErrorResponse("Failed to retrieve project"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> CreateProject([FromBody] CreateProjectDto createProjectDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ProjectDto>.ErrorResponse("Validation failed", errors));
                }

                var project = await _projectService.CreateProjectAsync(createProjectDto);
                return CreatedAtAction(nameof(GetProject), new { id = project.Id },
                    ApiResponse<ProjectDto>.SuccessResponse(project, "Project created successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<ProjectDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project: {ProjectName}", createProjectDto.Name);
                return StatusCode(500, ApiResponse<ProjectDto>.ErrorResponse("Failed to create project"));
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(int id, [FromBody] UpdateProjectDto updateProjectDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(ApiResponse<ProjectDto>.ErrorResponse("Validation failed", errors));
                }

                var project = await _projectService.UpdateProjectAsync(id, updateProjectDto);
                if (project == null)
                {
                    return NotFound(ApiResponse<ProjectDto>.ErrorResponse("Project not found"));
                }

                return Ok(ApiResponse<ProjectDto>.SuccessResponse(project, "Project updated successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<ProjectDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project with ID {ProjectId}", id);
                return StatusCode(500, ApiResponse<ProjectDto>.ErrorResponse("Failed to update project"));
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProject(int id)
        {
            try
            {
                var result = await _projectService.DeleteProjectAsync(id);
                if (!result)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("Project not found"));
                }

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Project deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project with ID {ProjectId}", id);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Failed to delete project"));
            }
        }

        [HttpPost("{id}/cleanup")]
        public async Task<ActionResult<ApiResponse<bool>>> CleanupProjectRepository(int id)
        {
            try
            {
                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("Project not found"));
                }

                // You'll need to inject IRepositoryService into the controller
                var repositoryService = HttpContext.RequestServices.GetRequiredService<IRepositoryService>();
                var result = await repositoryService.CleanupRepositoryAsync(project.Name);

                if (result)
                {
                    return Ok(ApiResponse<bool>.SuccessResponse(true, "Repository cleaned up successfully"));
                }
                else
                {
                    return StatusCode(500, ApiResponse<bool>.ErrorResponse("Failed to cleanup repository"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up repository for project with ID {ProjectId}", id);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Failed to cleanup repository"));
            }
        }

        [HttpPost("{id}/force-sync")]
        public async Task<ActionResult<ApiResponse<bool>>> ForceSyncProject(int id)
        {
            try
            {
                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("Project not found"));
                }

                var repositoryService = HttpContext.RequestServices.GetRequiredService<IRepositoryService>();
                await repositoryService.ForceCloneRepositoryAsync(
                    project.RepositoryUrl,
                    project.RepositoryType,
                    project.Name);

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Project force synchronized successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error force syncing project with ID {ProjectId}", id);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Failed to force sync project"));
            }
        }
    }
}