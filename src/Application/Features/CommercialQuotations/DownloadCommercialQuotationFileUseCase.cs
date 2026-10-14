using SnG.Control.Application.Files;
using SnG.Control.Application.Identity;

namespace SnG.Control.Application.Features.CommercialQuotations;

public sealed class DownloadCommercialQuotationFileUseCase(ICommercialQuotationStore store, IFileStorage storage,
    ICommercialHeadIdentity head, ICurrentUser user)
{
    public async Task<CommercialQuotationDownloadResult> ExecuteAsync(Guid projectId, CancellationToken token)
    {
        var state = await store.GetFileAsync(projectId, token);
        if (state is null) return new(CommercialQuotationStatus.ProjectNotFound);
        if (!string.Equals(user.Subject, head.Subject, StringComparison.Ordinal)
            && !string.Equals(user.Subject, state.Project.CreatedByGoogleSubject, StringComparison.Ordinal)) return new(CommercialQuotationStatus.Forbidden);
        if (state.Quotation is null) return new(CommercialQuotationStatus.DocumentUnavailable);
        return new(CommercialQuotationStatus.Success, await storage.ReadAsync(state.Quotation.FileStorageKey, token), state.Quotation.OriginalFileName);
    }
}
