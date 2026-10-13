namespace SnG.Control.Infrastructure.Quotations;

public sealed class BudgetQuotationParserOptions
{
    public const string SectionName = "BudgetQuotationPreview";
    // Technical safeguards, configurable independently of the document/business contract.
    public long MaxUploadBytes { get; set; } = 30_000_000;
    public long MaxUncompressedBytes { get; set; } = 134_217_728;
    public int MaxZipEntries { get; set; } = 10_000;
}
