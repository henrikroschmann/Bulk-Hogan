using Microsoft.EntityFrameworkCore;

namespace BulkHoganTests;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; }


}
