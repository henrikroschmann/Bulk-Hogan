namespace BulkHoganTests;

public class Customer
{
    public long Id { get; set; }
    public string Name { get; set; }

    public static Customer Create(long id, string name) =>
        new Customer { Id = id, Name = name };
}