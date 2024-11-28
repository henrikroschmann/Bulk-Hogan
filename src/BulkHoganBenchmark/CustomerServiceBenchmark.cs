using BenchmarkDotNet.Attributes;
using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BulkHoganBenchmark;

[MemoryDiagnoser]
public class CustomerServiceBenchmark : IDisposable
{
    private Fixture _fixture;
    private ApplicationDbContext _context;
    private CustomerService _customerService;
    private readonly PostgreSqlContainer _postgres;
    private List<Customer> Customers;

    public CustomerServiceBenchmark()
    {
        _fixture = new Fixture();
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _postgres.StartAsync().GetAwaiter().GetResult();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(_postgres.GetConnectionString());

        _context = new ApplicationDbContext(optionsBuilder.Options);
        _context.Database.Migrate();

        _customerService = new CustomerService(_context);

        Customers = _fixture.Build<Customer>().With(x => x.Name, "frank").CreateMany(100)
            .ToList();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _postgres.DisposeAsync().GetAwaiter().GetResult();
        _context.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clean up the database before each iteration
        _context.Customers.RemoveRange(_context.Customers);
        _context.SaveChanges();
    }

    [Benchmark]
    public void InsertCustomersIndividually()
    {
        foreach (var customer in Customers)
        {
            _customerService.Create(customer);
        }
    }

    [Benchmark]
    public void InsertCustomersInBulk()
    {
        _customerService.CreateRange(Customers);
    }

    [Benchmark]
    public async Task InsertCustomersInBulkOperationAsync()
    {
        await _customerService.CreateRangeInBulkAsync(Customers);
    }

    [Benchmark]
    public async Task InsertCustomesInBulkOperationWithConditionAsync()
    {

        await _customerService.CreateRangeInBulkWithConditionAsync(Customers);

    }
}
