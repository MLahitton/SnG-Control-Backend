using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using SnG.Control.Api.Filters;
using SnG.Control.Application.Features.Budgets.PreviewQuotation;
using SnG.Control.Infrastructure.Quotations;

namespace SnG.Control.Api.Controllers;

[ApiController]
[Route("api/projects/{id}/budget-quotation")]
public class BudgetQuotationController(PreviewBudgetQuotationUseCase preview, IOptions<BudgetQuotationParserOptions> options) : ControllerBase
{
    [HttpPost("preview")]
    [DisableFormValueModelBinding]
    [ProducesResponseType<BudgetQuotationPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BudgetQuotationPreviewResponse>> Preview(Guid id, CancellationToken cancellationToken)
    {
        // Read multipart directly: no IFormFile buffering to temporary files or local path input.
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var contentType)
            || !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)) return InvalidUpload();
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > 128) return InvalidUpload();
        using var buffer = new MemoryStream();
        string fileName;
        try
        {
            var reader = new MultipartReader(boundary, Request.Body) { BodyLengthLimit = options.Value.MaxUploadBytes };
            var part = await reader.ReadNextSectionAsync(cancellationToken);
            if (part is null || !ContentDispositionHeaderValue.TryParse(part.ContentDisposition, out var disposition)
                || !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase)
                || HeaderUtilities.RemoveQuotes(disposition.Name).Value != "file") return InvalidUpload();
            fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value ?? "";
            if (fileName.Length == 0) return InvalidUpload();
            var bytes = new byte[81920];
            int read;
            while ((read = await part.Body.ReadAsync(bytes, cancellationToken)) > 0)
            {
                if (buffer.Length + read > options.Value.MaxUploadBytes) return InvalidUpload();
                await buffer.WriteAsync(bytes.AsMemory(0, read), cancellationToken);
            }
            if (buffer.Length == 0 || await reader.ReadNextSectionAsync(cancellationToken) is not null) return InvalidUpload();
        }
        catch (InvalidDataException) { return InvalidUpload(); }
        catch (IOException) { return InvalidUpload(); }
        buffer.Position = 0;
        var result = await preview.ExecuteAsync(id, buffer, fileName, cancellationToken);
        return result switch
        {
            PreviewBudgetQuotationResult.Success success => Ok(success.Response),
            PreviewBudgetQuotationResult.ProjectNotFound => Problem(statusCode: 404, title: "Project not found."),
            PreviewBudgetQuotationResult.InvalidWorkflowState => Problem(statusCode: 409, title: "El proyecto no está listo para el preview de Presupuestos."),
            PreviewBudgetQuotationResult.SgCodeMismatch => Problem(statusCode: 409, title: "El SG del archivo no coincide con el proyecto."),
            PreviewBudgetQuotationResult.InvalidWorkbook invalid => Problem(statusCode: 400,
                title: "El archivo no corresponde a una cotización de Presupuestos legible y soportada.",
                extensions: new Dictionary<string, object?> { ["code"] = invalid.Error.ToString() }),
            _ => throw new InvalidOperationException("Unexpected preview result.")
        };
    }

    private ObjectResult InvalidUpload() => Problem(statusCode: 400, title: "Envíe un único archivo XLSX en el campo multipart file, dentro de los límites técnicos configurados.");
}
