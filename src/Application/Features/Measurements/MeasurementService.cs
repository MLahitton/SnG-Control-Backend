using SnG.Control.Application.Access;
using SnG.Control.Application.Features.ClientQuotationDecisions;
using SnG.Control.Application.Features.ProjectDocuments;
using SnG.Control.Application.Identity;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
namespace SnG.Control.Application.Features.Measurements;

public sealed record MeasurementCommand(Guid RequestKey, long ExpectedRevision, string Action, Guid? StructureId = null,
    bool? Measured = null, string? Description = null, decimal? Width = null, decimal? Height = null, string? Note = null, Guid[]? DocumentIds = null);
public sealed record MeasurementSummary(int Initial, int Added, int Retired, int Active, int Measured, int Pending, decimal Progress)
{
    public static MeasurementSummary From(IReadOnlyList<MeasurementStructure> rows)
    {
        var initial = rows.Count(s => s.CommercialQuotationItemId is not null); var added = rows.Count - initial;
        var retired = rows.Count(s => s.RetiredAt is not null); var active = rows.Count - retired;
        var measured = rows.Count(s => s.RetiredAt is null && s.MeasuredAt is not null);
        return new(initial, added, retired, active, measured, active - measured, active == 0 ? 0 : decimal.Round(100m * measured / active, 2));
    }
}
public sealed record MeasurementStructureView(Guid Id, Guid? ItemId, int UnitOrdinal, int UnitCount, string? ItemNumber,
    string Description, string? Location, decimal Width, decimal Height, string? Note, DateTime CreatedAt, DateTime? MeasuredAt, DateTime? RetiredAt)
{
    public static MeasurementStructureView From(MeasurementStructure s) => new(s.Id, s.CommercialQuotationItemId, s.UnitOrdinal, s.UnitCount,
        s.ItemNumber, s.Description, s.Location, s.Width, s.Height, s.Note, s.CreatedAt, s.MeasuredAt, s.RetiredAt);
}
public sealed record MeasurementHistory(Guid Id, string Action, Guid? StructureId, DateTime At, DocumentPerson Actor, long Revision);
public sealed record MeasurementSnapshot(Guid CommercialQuotationId, int QuotationRound, MeasurementSummary Summary,
    IReadOnlyList<MeasurementStructureView> Structures, IReadOnlyList<Guid> DocumentIds);
public sealed record MeasurementState(Guid ProjectId, Guid TaskId, string TaskStatus, string ResponsibleSubject, bool CanModify,
    Guid? ChecklistId, long Revision, MeasurementSummary Summary, IReadOnlyList<MeasurementStructureView> Structures,
    IReadOnlyList<DocumentResponse> Documents, IReadOnlyList<MeasurementHistory> History,
    Guid? SubmissionId, DateTime? SubmittedAt, MeasurementSnapshot? Snapshot);
public interface IMeasurementStore
{
    Task<MeasurementState?> ReadAsync(Guid id, ProjectReadScope scope, CancellationToken token);
    Task InitializeAsync(Guid id, DecisionActor actor, CancellationToken token);
    Task ExecuteAsync(Guid id, MeasurementCommand command, DecisionActor actor, CancellationToken token);
}
public interface IMeasurementDocumentStore
{
    Task RequireMeasurementUploadAccessAsync(Guid id, CancellationToken token);
    Task<DocumentBatchResponse> UploadMeasurementsAsync(Guid id, IAsyncEnumerable<DocumentUpload> uploads, CancellationToken token, Guid? requestKey = null);
}
public sealed class MeasurementService(IMeasurementStore store, ICurrentUser current, IActiveRoleContext active, IInstallationAssigneeIdentity installation)
{
    private DecisionActor Actor => new(current.Subject, current.Email, current.Name);
    public Task<MeasurementState?> ReadAsync(Guid id, CancellationToken token) => store.ReadAsync(id, new(current.Subject, active.Role), token);
    public Task InitializeAsync(Guid id, CancellationToken token) { RequireWrite(); return store.InitializeAsync(id, Actor, token); }
    public Task ExecuteAsync(Guid id, MeasurementCommand command, CancellationToken token)
    {
        RequireWrite();
        if (command.RequestKey == Guid.Empty || command.ExpectedRevision < 0) throw new DocumentException(400, "InvalidMeasurementCommand");
        var valid = command.Action switch {
            "Measure" => command.StructureId is { } sid && sid != Guid.Empty && command.Measured is not null && command.Description is null && command.Width is null && command.Height is null && command.Note is null && command.DocumentIds is null,
            "Retire" => command.StructureId is { } rid && rid != Guid.Empty && command.Measured is null && command.Description is null && command.Width is null && command.Height is null && command.Note is null && command.DocumentIds is null,
            "Add" => command.StructureId is null && command.Measured is null && !string.IsNullOrWhiteSpace(command.Description) && command.Description.Length <= 2000 && command.Width > 0 && command.Height > 0 && command.Note?.Length is not > 4000 && command.DocumentIds is null,
            "Submit" => command.StructureId is null && command.Measured is null && command.Description is null && command.Width is null && command.Height is null && command.Note is null && command.DocumentIds is { Length: > 0 and <= ProjectDocument.MaxCurrentDocuments } ids && ids.All(x => x != Guid.Empty) && ids.Distinct().Count() == ids.Length,
            _ => false
        };
        if (!valid) throw new DocumentException(400, "InvalidMeasurementCommand");
        return store.ExecuteAsync(id, command, Actor, token);
    }
    private void RequireWrite()
    {
        if (active.Role != SystemRole.Installation || string.IsNullOrWhiteSpace(installation.Subject) || current.Subject != installation.Subject)
            throw new DocumentException(403, "MeasurementAccessDenied");
    }
}
