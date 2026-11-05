using Microsoft.AspNetCore.Mvc;
using SnG.Control.Api.Contracts;
using SnG.Control.Api.Filters;
using SnG.Control.Application.Features.Engineering;
using SnG.Control.Domain.Entities;
using SnG.Control.Infrastructure.Persistence.Stores;
namespace SnG.Control.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/projects/{id:guid}/opening-records")]
public sealed class OpeningRecordsController(OpeningRecordService service) : ControllerBase
{
    public sealed record HistoricalRequest(Guid ChecklistId, string Reason);
    [HttpGet]
    public async Task<IActionResult> Read(Guid id, CancellationToken token) => await service.ReadAsync(id, token) is { } state ? Ok(state) : NotFound();
    [HttpPost("rounds/{roundId:guid}/commands")]
    public async Task<IActionResult> Command(Guid id, Guid roundId, OpeningCommand command, CancellationToken token)
    { await service.CommandAsync(id, roundId, command, token); return NoContent(); }
    [HttpPost("rounds/{roundId:guid}/documents")]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(EfEngineeringStore.MaxPdfBytes + 100_000)]
    public async Task<IActionResult> Upload(Guid id, Guid roundId, [FromQuery] Guid requestKey, [FromQuery] long expectedRevision, [FromQuery] OpeningDocumentKind kind, CancellationToken token)
    { service.RequireWrite(); await service.UploadAsync(id, roundId, requestKey, expectedRevision, kind, ProjectDocumentMultipartReader.ReadAsync(Request, token), token); return NoContent(); }
    [HttpGet("documents/{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid id, Guid documentId, CancellationToken token)
    { var file = await service.DownloadAsync(id, documentId, token); Response.Headers["X-Content-Type-Options"] = "nosniff"; return File(file.Content, "application/pdf", file.FileName); }
    [HttpPost("initialize-historical")]
    public async Task<IActionResult> Initialize(Guid id, HistoricalRequest command, CancellationToken token)
    { await service.InitializeHistoricalAsync(id, command.ChecklistId, command.Reason, token); return NoContent(); }
}
