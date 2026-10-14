using SnG.Control.Application.Features.Budgets.ConfirmQuotation;
using SnG.Control.Application.Features.Budgets;

namespace SnG.Control.Tests.TestDoubles;

public sealed class QuotationConfirmationStoreDouble(ProjectStoreDouble projects, ProjectTaskStoreDouble tasks) : IBudgetQuotationConfirmationStore
{
    public Dictionary<Guid, BudgetQuotationConfirmation> Confirmations { get; } = [];
    public CancellationToken Token { get; private set; }
    public int Calls { get; private set; }
    public Task<QuotationConfirmationStoreResult> ConfirmAsync(BudgetQuotationConfirmation confirmation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Token = cancellationToken; Calls++;
        lock (Confirmations)
        {
            var project = projects.Items.SingleOrDefault(project => project.Id == confirmation.Version.ProjectId);
            if (project is null) return Task.FromResult(new QuotationConfirmationStoreResult(QuotationConfirmationStatus.ProjectNotFound));
            var task = BudgetQuotationWorkflow.ReadyTask(project, tasks.Items.Where(task => task.ProjectId == project.Id));
            if (task is null) return Task.FromResult(new QuotationConfirmationStoreResult(QuotationConfirmationStatus.InvalidWorkflowState));
            if (project.SgCode != confirmation.Version.DocumentSgCode) return Task.FromResult(new QuotationConfirmationStoreResult(QuotationConfirmationStatus.SgCodeMismatch));
            if (!Confirmations.TryAdd(project.Id, confirmation)) return Task.FromResult(new QuotationConfirmationStoreResult(QuotationConfirmationStatus.AlreadyConfirmed));
            return Task.FromResult(new QuotationConfirmationStoreResult(QuotationConfirmationStatus.Created, task));
        }
    }
}
