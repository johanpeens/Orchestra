using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamPortal.Domain.Entities;

namespace TeamPortal.Infrastructure.Data.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ActivityLog");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).HasConversion<int>().IsRequired();
        builder.Property(x => x.EntityName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(500);

        // SetNull, not Cascade: history outlives the user who made it.
        builder.HasOne(x => x.User)
         .WithMany(u => u.ActivityLogs)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.SetNull);

        // The log always reads newest first.
        builder.HasIndex(x => x.HappenedAt).IsDescending();
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Notification");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.Property(x => x.LinkUrl).HasMaxLength(500);

        builder.HasOne(x => x.User)
         .WithMany(u => u.Notifications)
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // Backs the unread badge count.
        builder.HasIndex(x => new { x.UserId, x.IsRead });
    }
}
