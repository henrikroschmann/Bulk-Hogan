using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;

namespace EfficientBulkOperations;

public class BulkOptions<T>
{
    public Expression<Func<T, T, bool>> MergeCondition { get; set; }
    public int? BatchSize { get; set; }
    public ConflictAction OnConflict { get; set; } = ConflictAction.DoUpdate;
}
