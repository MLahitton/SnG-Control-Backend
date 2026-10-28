using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SnG.Control.Api.Authorization;
using SnG.Control.Application.Features.Accounting;
namespace SnG.Control.Api.Controllers;

public sealed record InitialDepositRequest([Required, MaxLength(48)] string Amount, [Required, MaxLength(40)] string ReceivedAt,
    [MaxLength(200)] string? Reference, [MaxLength(2000)] string? Note);
public sealed record AccountingHandoffRequest;
[ApiController]
[Route("api/projects/{id}")]
public sealed class AccountingController(AccountingService service) : ControllerBase
{
    [HttpGet("initial-deposit")]
    [ProducesResponseType<AccountingState>(200)]
    public async Task<IActionResult> Read(Guid id, CancellationToken token) => await service.ReadAsync(id, token) is { } result ? Ok(result) : Failure(AccountingStatus.ProjectNotFound);
    [HttpPost("send-approved-quotation-to-accounting")]
    [Authorize(Policy = ActiveRoleRegistration.CommercialAdvisorPolicy)]
    [ProducesResponseType<AccountingWriteResponse>(201)]
    public async Task<IActionResult> Send(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AccountingHandoffRequest? body, CancellationToken token) => Result(await service.SendAsync(id, token));
    [HttpPost("initial-deposit")]
    [Authorize(Policy = AccountingAssigneeRequirement.PolicyName)]
    [ProducesResponseType<AccountingWriteResponse>(201)]
    public async Task<IActionResult> Record(Guid id, InitialDepositRequest body, CancellationToken token) => Result(await service.RecordAsync(id, body.Amount, body.ReceivedAt, body.Reference, body.Note, token));
    private IActionResult Result(AccountingResult result) => result.Status == AccountingStatus.Success ? StatusCode(201, result.Response) : Failure(result.Status);
    private ObjectResult Failure(AccountingStatus status) => Problem(statusCode: status switch { AccountingStatus.Forbidden => 403, AccountingStatus.ProjectNotFound => 404, AccountingStatus.InvalidDeposit => 400, _ => 409 },
        title: "Accounting operation unavailable", extensions: new Dictionary<string, object?> { ["code"] = status.ToString() });
}
