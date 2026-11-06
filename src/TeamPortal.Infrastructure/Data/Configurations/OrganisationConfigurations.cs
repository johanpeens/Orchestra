using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPortal.Domain.Entities;

namespace TeamPortal.Infrastructure.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Department");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ColourHex).IsRequired().HasMaxLength(7);

        // Filtered so a soft-deleted department does not reserve its name.
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.SortOrder);
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Employee");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.JobTitle).HasMaxLength(150);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.PhotoUrl).HasMaxLength(500);
        builder.Property(x => x.StaffType).HasConversion<int>().IsRequired();

        builder.HasOne(x => x.Department)
         .WithMany(d => d.Employees)
         .HasForeignKey(x => x.DepartmentId)
         .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Email)
         .IsUnique()
         .HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(x => new { x.DepartmentId, x.IsActive });
    }
}
