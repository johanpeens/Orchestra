using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPortal.Domain.Entities;

namespace TeamPortal.Infrastructure.Data.Configurations;

public class ProcessFolderConfiguration : IEntityTypeConfiguration<ProcessFolder>
{
    public void Configure(EntityTypeBuilder<ProcessFolder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProcessFolder");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class StepStatusConfiguration : IEntityTypeConfiguration<StepStatus>
{
    public void Configure(EntityTypeBuilder<StepStatus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StepStatus");
        builder.HasKey(x => x.Id);

        // A lookup table, so ids are assigned by the seeder, not the database.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ColourHex).IsRequired().HasMaxLength(7);

        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public class ProcessConfiguration : IEntityTypeConfiguration<Process>
{
    public void Configure(EntityTypeBuilder<Process> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Process");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasOne(x => x.ProcessFolder)
         .WithMany(f => f.Processes)
         .HasForeignKey(x => x.ProcessFolderId)
         .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Department)
         .WithMany(d => d.Processes)
         .HasForeignKey(x => x.DepartmentId)
         .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StepStatus)
         .WithMany(s => s.Processes)
         .HasForeignKey(x => x.StepStatusId)
         .OnDelete(DeleteBehavior.Restrict);

        // Name is unique within its folder, not globally.
        builder.HasIndex(x => new { x.ProcessFolderId, x.Name })
         .IsUnique()
         .HasFilter("[IsDeleted] = 0");

        // SortOrder is the flow-strip position, so it must not tie or gap.
        builder.HasIndex(x => new { x.ProcessFolderId, x.SortOrder })
         .IsUnique()
         .HasFilter("[IsDeleted] = 0");
    }
}

public class ProcessStepConfiguration : IEntityTypeConfiguration<ProcessStep>
{
    public void Configure(EntityTypeBuilder<ProcessStep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProcessStep");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Description).HasMaxLength(2000);

        builder.HasOne(x => x.Process)
         .WithMany(p => p.Steps)
         .HasForeignKey(x => x.ProcessId)
         .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Department)
         .WithMany(d => d.ProcessSteps)
         .HasForeignKey(x => x.DepartmentId)
         .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StepStatus)
         .WithMany(s => s.ProcessSteps)
         .HasForeignKey(x => x.StepStatusId)
         .OnDelete(DeleteBehavior.Restrict);

        // No two live steps in one process may share a number.
        builder.HasIndex(x => new { x.ProcessId, x.StepNumber })
         .IsUnique()
         .HasFilter("[IsDeleted] = 0");
    }
}

public class StepOwnerConfiguration : IEntityTypeConfiguration<StepOwner>
{
    public void Configure(EntityTypeBuilder<StepOwner> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StepOwner");

        // Composite key - one person can only be added to a step once.
        builder.HasKey(x => new { x.ProcessStepId, x.EmployeeId });

        builder.HasOne(x => x.ProcessStep)
         .WithMany(s => s.Owners)
         .HasForeignKey(x => x.ProcessStepId)
         .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Employee)
         .WithMany(e => e.StepOwners)
         .HasForeignKey(x => x.EmployeeId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StepImageConfiguration : IEntityTypeConfiguration<StepImage>
{
    public void Configure(EntityTypeBuilder<StepImage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StepImage");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).IsRequired().HasMaxLength(260);
        builder.Property(x => x.StoredPath).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);

        builder.HasOne(x => x.ProcessStep)
         .WithMany(s => s.Images)
         .HasForeignKey(x => x.ProcessStepId)
         .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UploadedByUser)
         .WithMany()
         .HasForeignKey(x => x.UploadedByUserId)
         .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ProcessStepId, x.SortOrder });
    }
}
