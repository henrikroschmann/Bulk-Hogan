using EfficientBulkOperations;

namespace EfficientBulkOperationsTests;

internal sealed class CustomerService(ApplicationDbContext dbContext)
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public IEnumerable<Customer> GetCustomers()
    {
        return _dbContext.Customers.ToList();
    }

    public async Task CreateAsync(Customer customer)
    {
        _dbContext.Add(customer);
        await _dbContext.SaveChangesAsync();
    }

    public async Task CreateInBuld(List<Customer> customers)
    {
        //var options = new BulkOptions<Customer>
        //{
        //    Transaction = _dbContext.Database.BeginTransaction(),
        //};
        await _dbContext.BulkInsertAsync(customers);
       
    }

    internal async Task CreateInBuldWithCondition(List<Customer> customers)
    {
        var options = new BulkOptions<Customer>
        {
            MergeCondition = (current, incoming) => current.Name == incoming.Name,
            OnConflict = ConflictAction.DoNothing,
        };
        await _dbContext.BulkInsertAsync<Customer>(customers, options);
    }
}
