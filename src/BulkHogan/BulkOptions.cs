using System.Linq.Expressions;

namespace BulkHogan;

public class BulkOptions<T>
{
    public Expression<Func<T, T, bool>> MergeCondition { get; set; }
    // Maybe add treshhold? 
    public ConflictAction OnConflict { get; set; } = ConflictAction.DoUpdate;
}
