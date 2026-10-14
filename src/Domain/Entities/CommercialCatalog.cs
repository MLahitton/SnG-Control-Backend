namespace SnG.Control.Domain.Entities;

public sealed class CommercialCatalog
{
    private CommercialCatalog() { }
    public CommercialCatalog(Guid budgetQuotationVersionId)
    {
        if (budgetQuotationVersionId == Guid.Empty) throw new ArgumentException("Quotation version cannot be empty.");
        Id = Guid.NewGuid(); BudgetQuotationVersionId = budgetQuotationVersionId;
        var now = DateTime.UtcNow;
        CreatedAt = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }
    public Guid Id { get; private set; }
    public Guid BudgetQuotationVersionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
