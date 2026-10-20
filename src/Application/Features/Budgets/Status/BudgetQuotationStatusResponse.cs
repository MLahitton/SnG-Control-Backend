using SnG.Control.Application.Features.Budgets.CommercialHandoff;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.Budgets.Status;

public enum BudgetQuotationStatus { NotConfirmed = 1, Confirmed = 2, SentToCommercial = 3 }
public enum BudgetQuotationSendBlockReason { None = 0, SgChangedAfterConfirmation = 1, InvalidState = 2 }

public sealed record BudgetQuotationStatusResponse(Guid ProjectId, BudgetQuotationStatus Status,
    int? VersionNumber, DateTime? ConfirmedAt, int ItemCount, long TotalQuantity,
    string? DocumentSgCode, string? CurrentProjectSgCode, bool CanSendToCommercial,
    BudgetQuotationSendBlockReason SendBlockReason)
{
    public static BudgetQuotationStatusResponse From(Project project, IReadOnlyList<ProjectTask> tasks,
        CommercialHandoffReadiness facts, DateTime? confirmedAt, long totalQuantity)
    {
        if (facts.VersionNumber is null)
            return new(project.Id, BudgetQuotationStatus.NotConfirmed, null, null, 0, 0, null,
                project.SgCode, false, BudgetQuotationSendBlockReason.None);
        var readiness = facts.Validate(project, tasks);
        var sent = readiness == CommercialHandoffStatus.AlreadySent;
        var reason = readiness switch
        {
            CommercialHandoffStatus.SgChangedAfterConfirmation => BudgetQuotationSendBlockReason.SgChangedAfterConfirmation,
            CommercialHandoffStatus.InvalidState => BudgetQuotationSendBlockReason.InvalidState,
            _ => BudgetQuotationSendBlockReason.None
        };
        return new(project.Id, sent ? BudgetQuotationStatus.SentToCommercial : BudgetQuotationStatus.Confirmed,
            facts.VersionNumber, confirmedAt, facts.ItemCount, totalQuantity, facts.DocumentSgCode,
            project.SgCode, readiness == CommercialHandoffStatus.Sent, reason);
    }
}
public interface IBudgetQuotationStatusStore
{
    Task<BudgetQuotationStatusResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken);
}