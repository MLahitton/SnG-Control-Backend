namespace SnG.Control.Domain.Entities;

public sealed class MeasurementChecklist
{
    private MeasurementChecklist() { }
    public MeasurementChecklist(Guid projectId, Guid quotationId, int round, Guid taskId, DateTime at, string subject)
    {
        if (projectId == Guid.Empty || quotationId == Guid.Empty || taskId == Guid.Empty || round < 1) throw new ArgumentException("Invalid checklist source.");
        ValidateActor(at, subject);
        Id = Guid.NewGuid(); ProjectId = projectId; CommercialQuotationId = quotationId; QuotationRound = round; TaskId = taskId;
        CreatedAt = at; CreatedBySubject = subject;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid CommercialQuotationId { get; private set; }
    public int QuotationRound { get; private set; }
    public Guid TaskId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBySubject { get; private set; } = "";
    public long Revision { get; private set; }
    public Guid? SubmissionId { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public string? SubmittedBySubject { get; private set; }
    public string? SnapshotJson { get; private set; }
    public void Advance()
    {
        if (SubmissionId is not null) throw new InvalidOperationException("Measurements already submitted.");
        Revision = checked(Revision + 1);
    }
    public void Submit(DateTime at, string subject, string snapshot)
    {
        ValidateActor(at, subject);
        if (at < CreatedAt || string.IsNullOrWhiteSpace(snapshot)) throw new ArgumentException("Invalid submission.");
        Advance(); SubmissionId = Guid.NewGuid(); SubmittedAt = at; SubmittedBySubject = subject; SnapshotJson = snapshot;
    }
    internal static void ValidateActor(DateTime at, string subject)
    {
        if (at.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
    }
}

public sealed class MeasurementStructure
{
    private MeasurementStructure() { }
    private MeasurementStructure(MeasurementChecklist checklist, DateTime at, string subject)
    {
        MeasurementChecklist.ValidateActor(at, subject);
        if (checklist.SubmissionId is not null) throw new InvalidOperationException("Measurements already submitted.");
        Id = Guid.NewGuid(); ChecklistId = checklist.Id; ProjectId = checklist.ProjectId; CreatedAt = at; CreatedBySubject = subject;
    }
    public static MeasurementStructure Quoted(MeasurementChecklist checklist, CommercialQuotationItem item, int ordinal, DateTime at, string subject)
    {
        if (item.CommercialQuotationId != checklist.CommercialQuotationId || ordinal < 1 || ordinal > item.Quantity) throw new ArgumentException("Invalid quotation unit.");
        return new(checklist, at, subject) { CommercialQuotationItemId = item.Id, UnitOrdinal = ordinal, UnitCount = item.Quantity,
            ItemNumber = item.ItemNumber, Description = item.System, Location = item.Location, Width = item.Width, Height = item.Height, Note = item.Observations };
    }
    public static MeasurementStructure Added(MeasurementChecklist checklist, string description, decimal width, decimal height, string? note, DateTime at, string subject)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length > 2000 || width <= 0 || height <= 0 || note?.Length > 4000) throw new ArgumentException("Invalid onsite structure.");
        return new(checklist, at, subject) { Description = description.Trim(), Width = width, Height = height, Note = note, UnitOrdinal = 1, UnitCount = 1 };
    }
    public Guid Id { get; private set; }
    public Guid ChecklistId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? CommercialQuotationItemId { get; private set; }
    public int UnitOrdinal { get; private set; }
    public int UnitCount { get; private set; }
    public string? ItemNumber { get; private set; }
    public string Description { get; private set; } = "";
    public string? Location { get; private set; }
    public decimal Width { get; private set; }
    public decimal Height { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBySubject { get; private set; } = "";
    public DateTime? MeasuredAt { get; private set; }
    public string? MeasuredBySubject { get; private set; }
    public DateTime? RetiredAt { get; private set; }
    public string? RetiredBySubject { get; private set; }
    public void Measure(bool measured, DateTime at, string subject)
    {
        ValidateChange(at, subject); MeasuredAt = measured ? at : null; MeasuredBySubject = measured ? subject : null;
    }
    public void Retire(DateTime at, string subject)
    {
        ValidateChange(at, subject); RetiredAt = at; RetiredBySubject = subject;
    }
    private void ValidateChange(DateTime at, string subject)
    {
        MeasurementChecklist.ValidateActor(at, subject);
        if (RetiredAt is not null || at < CreatedAt || at < MeasuredAt) throw new InvalidOperationException("Invalid structure transition.");
    }
}

public sealed class MeasurementEvent
{
    private MeasurementEvent() { }
    public MeasurementEvent(MeasurementChecklist checklist, Guid requestKey, string fingerprint, string action, Guid? structureId, DateTime at, string subject, string? email, string? name)
    {
        MeasurementChecklist.ValidateActor(at, subject);
        if (requestKey == Guid.Empty) throw new ArgumentException("Request key required.");
        Id = Guid.NewGuid(); ChecklistId = checklist.Id; ProjectId = checklist.ProjectId; RequestKey = requestKey; Fingerprint = fingerprint;
        Action = action; StructureId = structureId; At = at; ActorSubject = subject; ActorEmail = email; ActorName = name; Revision = checklist.Revision;
    }
    public Guid Id { get; private set; }
    public Guid ChecklistId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid RequestKey { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string Action { get; private set; } = "";
    public Guid? StructureId { get; private set; }
    public long Revision { get; private set; }
    public DateTime At { get; private set; }
    public string ActorSubject { get; private set; } = "";
    public string? ActorEmail { get; private set; }
    public string? ActorName { get; private set; }
}

public sealed class MeasurementSubmissionDocument
{
    private MeasurementSubmissionDocument() { }
    public MeasurementSubmissionDocument(MeasurementChecklist checklist, ProjectDocument document)
    {
        if (checklist.SubmissionId is null || document.ProjectId != checklist.ProjectId || !document.IsMeasurementDocument || !document.IsCurrent) throw new ArgumentException("Invalid measurement document.");
        ChecklistId = checklist.Id; ProjectId = checklist.ProjectId; ProjectDocumentId = document.Id;
    }
    public Guid ChecklistId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ProjectDocumentId { get; private set; }
}
