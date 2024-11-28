using EfficientBulkOperations;

namespace EfficientBulkOperationsBenchmark;

internal class CustomerService(ApplicationDbContext dbContext)
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public void Create(Customer customer)
    {
        _dbContext.Add(customer);
        _dbContext.SaveChanges();
    }

    public void CreateRange(IEnumerable<Customer> customers)
    {
        _dbContext.AddRange(customers);
        _dbContext.SaveChanges();
    }

    internal async Task CreateRangeInBulkAsync(IEnumerable<Customer> customers)
    {
        await _dbContext.BulkInsertAsync<Customer>(customers);
    }

    internal async Task CreateRangeInBulkWithConditionAsync(IEnumerable<Customer> customers)
    {
        var options = new BulkOptions<Customer>
        {
            MergeCondition = (current, incoming) => current.Name == incoming.Name,
            OnConflict = ConflictAction.DoNothing,
        };
        await _dbContext.BulkInsertAsync<Customer>(customers, options);
    }
}