using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediaArchive.Data;

// `dotnet ef` needs to build an AppDbContext at design time, but the library has
// no app host to resolve it from. This factory gives the tooling a context bound
// to a local dev db; the running app supplies the real device path in the head.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=mediaarchive.db")
            .Options;

        return new AppDbContext(options);
    }
}
