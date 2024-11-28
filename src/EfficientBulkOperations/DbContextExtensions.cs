using Microsoft.EntityFrameworkCore;

namespace EfficientBulkOperations;

public static class DbContextExtensions
{
    public static Task BulkInsertAsync<T>(this DbContext context, IEnumerable<T> entities, BulkOptions<T> options = null)
    {
        return context.ExecuteBulkOperationAsync(entities, options);
    }
}
