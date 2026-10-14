using SnG.Control.Application.Features.ProjectTasks;
using SnG.Control.Domain.Entities;
using SnG.Control.Domain.Enums;
using SnG.Control.Domain.ValueObjects;

namespace SnG.Control.Application.Features.Budgets.CommercialHandoff;

public enum CommercialHandoffStatus { Sent, ProjectNotFound, InvalidState, SgChangedAfterConfirmation, AlreadySent, DocumentMismatch, DocumentUnavailable }

public sealed record CommercialHandoffState(Project Project, IReadOnlyList<ProjectTask> Tasks, BudgetQuotationVersion? Version,
    CommercialCatalog? Catalog, IReadOnlyList<CommercialCatalogItem> Items, IReadOnlyList<CommercialCatalogItemExtraction> Extractions)
{
    public CommercialHandoffStatus Validate()
    {
        if (Tasks.Any(task => task.Type == ProjectTaskType.ReviewQuotation) || Version?.ConfirmedFileStorageKey is not null
            || Tasks.Any(task => task.Type == ProjectTaskType.PrepareQuotation && task.Status == ProjectTaskStatus.Completed))
            return CommercialHandoffStatus.AlreadySent;
        if (BudgetQuotationWorkflow.ReadyTask(Project, Tasks) is null || Version is null || Version.VersionNumber != 1
            || Catalog is null || Items.Count == 0 || Extractions.Count != Items.Count
            || Extractions.Select(value => value.CatalogItemId).Distinct().Count() != Items.Count
            || Items.Any(item => !Extractions.Any(value => value.CatalogItemId == item.Id)))
            return CommercialHandoffStatus.InvalidState;
        if (!string.Equals(Project.SgCode, Version.DocumentSgCode, StringComparison.Ordinal))
            return CommercialHandoffStatus.SgChangedAfterConfirmation;
        return CommercialHandoffStatus.Sent;
    }

    public IReadOnlyList<ConfirmedWorkbookItem> WorkbookItems() => Items.OrderBy(item => item.SourceRow).Select(item =>
    {
        var original = Extractions.Single(value => value.CatalogItemId == item.Id);
        return new ConfirmedWorkbookItem(item.Section, item.SourceRow,
            new CatalogItemValues(original.ItemNumber, original.Location, original.System, original.Glass, original.AluminumFinish,
                original.LockType, original.Observations, original.Width, original.Height, original.SquareMeters, original.Quantity),
            new CatalogItemValues(item.ItemNumber, item.Location, item.System, item.Glass, item.AluminumFinish,
                item.LockType, item.Observations, item.Width, item.Height, item.SquareMeters, item.Quantity));
    }).ToArray();

    public bool SameDocumentAs(CommercialHandoffState other) => Version?.Id == other.Version?.Id && Catalog?.Id == other.Catalog?.Id
        && Version?.OriginalFileStorageKey == other.Version?.OriginalFileStorageKey && Version?.DocumentSgCode == other.Version?.DocumentSgCode
        && WorkbookItems().SequenceEqual(other.WorkbookItems());
}

public sealed record SendBudgetQuotationToCommercialResponse(Guid ProjectId, ProjectTaskResponse BudgetTask,
    ProjectTaskResponse CommercialTask, Guid QuotationVersionId, Guid CommercialCatalogId, bool CommercialFileAvailable);
public sealed record CommercialHandoffResult(CommercialHandoffStatus Status, SendBudgetQuotationToCommercialResponse? Response = null);
public sealed record CommercialFileState(bool ProjectExists, string? StorageKey = null, string? FileName = null, bool ReviewExists = false);

public interface IBudgetQuotationCommercialHandoffStore
{
    Task<CommercialHandoffState?> LoadAsync(Guid projectId, CancellationToken cancellationToken);
    Task<CommercialHandoffResult> SendAsync(CommercialHandoffState expected, ReadOnlyMemory<byte> workbook, CancellationToken cancellationToken);
    Task<CommercialFileState> GetFileAsync(Guid projectId, CancellationToken cancellationToken);
}
