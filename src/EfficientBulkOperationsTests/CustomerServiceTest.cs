using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EfficientBulkOperationsTests;

public sealed class CustomerServiceTest : IAsyncLifetime
{
    private readonly Fixture _fixture = new();
    private ApplicationDbContext? _context;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(_postgres.GetConnectionString());
        _context = new ApplicationDbContext(optionsBuilder.Options);
        _context.Database.Migrate();
    }

    public Task DisposeAsync()
    {
        return _postgres.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task ShouldReturnCustomersAsync()
    {
        // Given
        var customerService = new CustomerService(_context);

        // When
        var customers = _fixture.CreateMany<Customer>(10000);
        foreach (var customer in customers)
        {
            await customerService.CreateAsync(customer);
        }
        var customersList = customerService.GetCustomers();

        // Then
        Assert.Equal(customers.Count(), customersList.Count());
    }

    [Fact]
    public async Task ShouldReturnCustomersInBulkAsync()
    {
        // Given
        var customerService = new CustomerService(_context);

        // When
        var customers = _fixture.CreateMany<Customer>(10000).ToList();
        await customerService.CreateInBuld(customers);
        var customersList = customerService.GetCustomers();

        // Then
        Assert.Equal(customers.Count(), customersList.Count());
    }

    [Fact]
    public async Task ShouldReturn1CustomerInBulkAsync()
    {
        // Given
        var customerService = new CustomerService(_context);

        // When
        var customers = _fixture.CreateMany<Customer>(10000).ToList();
        await customerService.CreateInBuldWithCondition(customers);
        var customersList = customerService.GetCustomers();

        // Then
        Assert.Equal(1, customersList.Count());
    }
}