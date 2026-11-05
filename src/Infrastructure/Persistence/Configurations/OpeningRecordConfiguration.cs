using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;
namespace SnG.Control.Infrastructure.Persistence.Configurations;

public sealed class OpeningRecordRoundConfiguration : IEntityTypeConfiguration<OpeningRecordRound>
{
    public void Configure(EntityTypeBuilder<OpeningRecordRound> b)
    {
        b.ToTable("OpeningRecordRounds", t =>
        {
            t.HasCheckConstraint("CK_OpeningRound_State", "\"Number\" > 0 AND \"Revision\" >= 0 AND \"Status\" IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_OpeningRound_Presentation", "(\"Status\" = 1 AND \"PresentedAt\" IS NULL AND \"PresentedBySubject\" IS NULL) OR (\"Status\" <> 1 AND \"PresentedAt\" IS NOT NULL AND \"PresentedAt\" >= \"ReceivedAt\" AND \"PresentedBySubject\" IS NOT NULL)");
            t.HasCheckConstraint("CK_OpeningRound_Response", "(\"Status\" IN (1,2) AND \"RespondedAt\" IS NULL) OR (\"Status\" IN (3,4,5) AND \"RespondedAt\" IS NOT NULL AND \"RespondedAt\" >= \"PresentedAt\" AND \"RespondedBySubject\" IS NOT NULL)");
            t.HasCheckConstraint("CK_OpeningRound_Accepted", "\"Status\" <> 5 OR (\"SignedDocumentId\" IS NOT NULL AND \"FilingTaskId\" IS NOT NULL AND \"FinalSnapshotJson\" IS NOT NULL)");
            t.HasCheckConstraint("CK_OpeningRound_Reviewed", "\"Status\" NOT IN (3,4) OR (\"ReviewTaskId\" IS NOT NULL AND (\"Status\" <> 4 OR (\"RevisedDocumentId\" IS NOT NULL AND \"ReviewCompletedAt\" IS NOT NULL AND \"FinalSnapshotJson\" IS NOT NULL)))");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.HasAlternateKey(x => new { x.Id, x.ProjectId });
        b.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();
        b.HasIndex(x => x.ProjectId, "UX_OpeningRounds_Active").IsUnique().HasFilter("\"Status\" IN (1,2,3)");
        b.HasIndex(x => x.ProjectId, "UX_OpeningRounds_Accepted").IsUnique().HasFilter("\"Status\" = 5");
        b.Property(x => x.Revision).IsConcurrencyToken(); b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.GeneralObservations).HasMaxLength(4000); b.Property(x => x.FinalSnapshotJson).HasColumnType("jsonb");
        b.HasOne<EngineeringChecklist>().WithMany().HasForeignKey(x => new { x.ChecklistId, x.ProjectId }).HasPrincipalKey(x => new { x.Id, x.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectTask>().WithOne().HasForeignKey<OpeningRecordRound>(x => x.PresentationTaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectTask>().WithOne().HasForeignKey<OpeningRecordRound>(x => x.ReviewTaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectTask>().WithOne().HasForeignKey<OpeningRecordRound>(x => x.FilingTaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectDocument>().WithMany().HasForeignKey(x => new { x.ProjectId, x.PresentedDocumentId }).HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectDocument>().WithMany().HasForeignKey(x => new { x.ProjectId, x.RevisedDocumentId }).HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectDocument>().WithMany().HasForeignKey(x => new { x.ProjectId, x.SignedDocumentId }).HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OpeningRecordStructureConfiguration : IEntityTypeConfiguration<OpeningRecordStructure>
{
    public void Configure(EntityTypeBuilder<OpeningRecordStructure> b)
    {
        b.ToTable("OpeningRecordStructures", t => t.HasCheckConstraint("CK_OpeningStructure_State", "\"Revision\" >= 0 AND \"Approval\" IN (1,2,3) AND (\"Approval\" <> 2 OR \"ApprovalDocumentId\" IS NOT NULL) AND (\"Approval\" <> 3 OR \"ChangeDescription\" IS NOT NULL) AND (NOT \"ReviewCompleted\" OR \"Affected\")"));
        b.HasKey(x => new { x.RoundId, x.StructureId }); b.Property(x => x.Revision).IsConcurrencyToken(); b.Property(x => x.Approval).HasConversion<int>();
        b.Property(x => x.ChangeDescription).HasMaxLength(4000);
        b.HasOne<OpeningRecordRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MeasurementStructure>().WithMany().HasForeignKey(x => x.StructureId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectDocument>().WithMany().HasForeignKey(x => x.ApprovalDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OpeningRecordDocumentConfiguration : IEntityTypeConfiguration<OpeningRecordDocument>
{
    public void Configure(EntityTypeBuilder<OpeningRecordDocument> b)
    {
        b.ToTable("OpeningRecordDocuments", t => t.HasCheckConstraint("CK_OpeningDocument_State", "\"Version\" > 0 AND \"Kind\" IN (1,2)"));
        b.HasKey(x => x.DocumentId); b.Property(x => x.DocumentId).ValueGeneratedNever(); b.Property(x => x.Kind).HasConversion<int>();
        b.Property(x => x.Sha256).HasMaxLength(64); b.Property(x => x.AffectedIdsJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ProjectId, x.Kind, x.Version }).IsUnique();
        b.HasOne<OpeningRecordRound>().WithMany().HasForeignKey(x => new { x.RoundId, x.ProjectId }).HasPrincipalKey(x => new { x.Id, x.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectDocument>().WithOne().HasForeignKey<OpeningRecordDocument>(x => new { x.ProjectId, x.DocumentId }).HasPrincipalKey<ProjectDocument>(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class OpeningRecordEventConfiguration : IEntityTypeConfiguration<OpeningRecordEvent>
{
    public void Configure(EntityTypeBuilder<OpeningRecordEvent> b)
    {
        b.ToTable("OpeningRecordEvents"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.RoundId, x.RequestKey }).IsUnique(); b.HasIndex(x => new { x.RoundId, x.Revision }).IsUnique();
        b.Property(x => x.Fingerprint).HasMaxLength(64); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.DetailsJson).HasColumnType("jsonb");
        b.HasOne<OpeningRecordRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
    }
}
