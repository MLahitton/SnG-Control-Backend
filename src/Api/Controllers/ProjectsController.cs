using Microsoft.AspNetCore.Mvc;
using SnG.Control.Api.Contracts;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.Projects.Create;
using SnG.Control.Application.Features.Projects.GetById;
using SnG.Control.Application.Features.Projects.List;

namespace SnG.Control.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(
    CreateProjectUseCase create,
    GetProjectByIdUseCase getById,
    ListProjectsUseCase list) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await create.ExecuteAsync(request.ClientId, request.Name, request.City, request.ProductLine, cancellationToken);
        if (result is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Client not found.");
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await getById.ExecuteAsync(id, cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found.")
            : Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await list.ExecuteAsync(cancellationToken));
}
