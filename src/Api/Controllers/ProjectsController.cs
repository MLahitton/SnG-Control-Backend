using Microsoft.AspNetCore.Mvc;
using SnG.Control.Api.Contracts;
using SnG.Control.Application.Features.Projects;
using SnG.Control.Application.Features.Projects.Create;
using SnG.Control.Application.Features.Projects.GetById;
using SnG.Control.Application.Features.Projects.List;
using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Application.Features.ProjectTasks.SendToBudgets;
using SnG.Control.Application.Features.ProjectTasks.ListByProject;

namespace SnG.Control.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(
    CreateProjectUseCase create,
    GetProjectByIdUseCase getById,
    ListProjectsUseCase list,
    SendProjectToBudgetsUseCase sendToBudgets,
    ListProjectTasksByProjectUseCase listTasks) : ControllerBase
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

    [HttpPost("{id}/send-to-budgets")]
    [ProducesResponseType<ProjectTaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectTaskResponse>> SendToBudgets(Guid id, CancellationToken cancellationToken)
    {
        var result = await sendToBudgets.ExecuteAsync(id, cancellationToken);
        return result switch
        {
            SendProjectToBudgetsResult.Created created => StatusCode(StatusCodes.Status201Created, created.Task),
            SendProjectToBudgetsResult.ProjectNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound, title: "Project not found."),
            SendProjectToBudgetsResult.AlreadySent => Problem(
                statusCode: StatusCodes.Status409Conflict, title: "Project has already been sent to Budgets."),
            _ => throw new InvalidOperationException("Unexpected send-to-budgets result.")
        };
    }

    [HttpGet("{id}/tasks")]
    [ProducesResponseType<IReadOnlyList<ProjectTaskResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectTaskResponse>>> ListTasks(Guid id, CancellationToken cancellationToken)
    {
        var result = await listTasks.ExecuteAsync(id, cancellationToken);
        return result is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found.")
            : Ok(result);
    }
}
