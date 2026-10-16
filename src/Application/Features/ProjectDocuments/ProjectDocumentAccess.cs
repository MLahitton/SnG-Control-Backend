using SnG.Control.Application.Identity;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Application.Features.ProjectDocuments;

public sealed class ProjectDocumentAccess(ICurrentUser current, ICommercialHeadIdentity head, IBudgetsAssigneeIdentity budgets, IManagementIdentity management)
{
    public ICurrentUser Current => current;
    public string BudgetsSubject => budgets.Subject;
    public bool CanModify(Project project) => string.Equals(current.Subject, project.CreatedByGoogleSubject, StringComparison.Ordinal)
        || string.Equals(current.Subject, head.Subject, StringComparison.Ordinal);
    public bool CanRead(ProjectDocumentState state) => CanModify(state.Project)
        || string.Equals(current.Subject, management.Subject, StringComparison.Ordinal)
        || (state.PrepareQuotation is not null && string.Equals(current.Subject, budgets.Subject, StringComparison.Ordinal));
    public void RequireModify(Project project)
    {
        if (!CanModify(project)) throw new DocumentException(403, "DocumentAccessDenied");
    }
}
