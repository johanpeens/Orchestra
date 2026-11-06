using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TeamPortal.Infrastructure.Data;

public class PortalDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TeamPortal;Trusted_Connection=True;")
            .Options;

        // No signed-in user at design time - see the constructor note.
        return new PortalDbContext(options, currentUser: null);
    }
}
