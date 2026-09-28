namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary><see cref="IDataSeeder.Order"/> values: each step builds on the data of the previous ones.</summary>
public static class SeedOrder
{
    public const int Workforce = 0;
    public const int Catalog = 10;
    public const int Customers = 20;
    public const int Orders = 30;
    public const int Payments = 40;
    public const int Packing = 45;
    public const int Delivery = 50;
}
