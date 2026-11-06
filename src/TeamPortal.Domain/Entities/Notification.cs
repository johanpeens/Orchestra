using TeamPortal.Domain.Identity;

namespace TeamPortal.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public AppUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Message { get; set; }

    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
