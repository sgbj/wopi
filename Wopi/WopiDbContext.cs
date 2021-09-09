using Microsoft.EntityFrameworkCore;

namespace Wopi;

public class WopiDbContext : DbContext
{
    public WopiDbContext(DbContextOptions<WopiDbContext> options)
        : base(options)
    {
    }

    public DbSet<WopiFile> WopiFiles { get; set; } = default!;
}
