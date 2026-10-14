using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SnG.Control.Domain.Entities;

namespace SnG.Control.Infrastructure.Persistence.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    internal const string PrepareQuotationIndexName = "UX_ProjectTasks_ProjectId_PrepareQuotation";
    internal const string ReviewQuotationIndexName = "UX_ProjectTasks_ProjectId_ReviewQuotation";

    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).ValueGeneratedNever();
        builder.Property(task => task.ProjectId).IsRequired();
        builder.Property(task => task.Area).HasConversion<int>().IsRequired();
        builder.Property(task => task.Type).HasConversion<int>().IsRequired();
        builder.Property(task => task.Status).HasConversion<int>().IsRequired();
        builder.Property(task => task.AssignedAt).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(task => task.StartedAt).HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(task => task.CompletedAt).HasColumnType("timestamp with time zone").IsRequired(false);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(task => task.ProjectId);
        builder.HasIndex(task => new { task.Area, task.Status });
        builder.HasIndex(task => task.ProjectId, PrepareQuotationIndexName)
            .IsUnique()
            .HasFilter("\"Type\" = 1");
        builder.HasIndex(task => task.ProjectId, ReviewQuotationIndexName)
            .IsUnique().HasFilter("\"Type\" = 2");
    }
}
