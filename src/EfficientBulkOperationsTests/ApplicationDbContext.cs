using Microsoft.EntityFrameworkCore;

namespace EfficientBulkOperationsTests;

internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; }


}
