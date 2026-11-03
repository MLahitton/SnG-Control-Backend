using Microsoft.AspNetCore.Mvc;
using SnG.Control.Api.Contracts;
using SnG.Control.Api.Filters;
using SnG.Control.Application.Features.Measurements;
using SnG.Control.Application.Features.ProjectDocuments;
namespace SnG.Control.Api.Controllers;

[ApiController]
[Route("api/projects/{id:guid}/measurements")]
public sealed class MeasurementsController(MeasurementService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MeasurementState>(200)]
    public async Task<IActionResult> Read(Guid id, CancellationToken token) => await service.ReadAsync(id, token) is { } state ? Ok(state) : NotFound();
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize(Guid id, CancellationToken token)
    {
        await service.InitializeAsync(id, token); return NoContent();
    }
    [HttpPost("commands")]
    public async Task<IActionResult> Execute(Guid id, MeasurementCommand body, CancellationToken token)
    {
        await service.ExecuteAsync(id, body, token); return NoContent();
    }
    [HttpPost("documents/batch")]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(ProjectDocumentMultipartReader.MaxRequestBytes)]
    [ProducesResponseType<DocumentBatchResponse>(201)]
    public async Task<IActionResult> Upload(Guid id, [FromQuery] Guid requestKey, [FromServices] IMeasurementDocumentStore documents, CancellationToken token)
    {
        if (requestKey == Guid.Empty) throw new DocumentException(400, "InvalidDocumentBatch");
        await documents.RequireMeasurementUploadAccessAsync(id, token);
        try { return StatusCode(201, await documents.UploadMeasurementsAsync(id, ProjectDocumentMultipartReader.ReadAsync(Request, token), token, requestKey)); }
        catch (BadHttpRequestException e) when (e.StatusCode == 413) { throw new DocumentException(413, "BatchTooLarge"); }
    }
}
