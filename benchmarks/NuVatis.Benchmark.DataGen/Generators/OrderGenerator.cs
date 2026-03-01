using Bogus;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.DataGen.Generators;

public class OrderGenerator
{
    private readonly Faker<Order> _faker;

    public OrderGenerator(List<long> userIds, int seed = 42)
    {
        var statuses = new[] { "pending", "processing", "shipped", "delivered", "cancelled" };

        _faker = new Faker<Order>()
            .UseSeed(seed)
            .RuleFor(o => o.UserId, f => f.PickRandom(userIds))
            .RuleFor(o => o.OrderNumber, f => $"ORD-{f.Random.AlphaNumeric(10).ToUpper()}")
            .RuleFor(o => o.OrderStatus, f => f.PickRandom(statuses))
            .RuleFor(o => o.Subtotal, f => f.Random.Decimal(10, 1000))
            .RuleFor(o => o.DiscountAmount, (f, o) => o.Subtotal * f.Random.Decimal(0, 0.2m))
            .RuleFor(o => o.TaxAmount, (f, o) => o.Subtotal * 0.1m)
            .RuleFor(o => o.ShippingFee, f => f.Random.Decimal(5, 20))
            .RuleFor(o => o.TotalAmount, (f, o) => o.Subtotal - o.DiscountAmount + o.TaxAmount + o.ShippingFee)
            .RuleFor(o => o.CouponId, f => f.Random.Bool(0.2f) ? f.Random.Long(1, 1000) : (long?)null)
            .RuleFor(o => o.CreatedAt, f => f.Date.Between(DateTime.Now.AddYears(-3), DateTime.Now))
            .RuleFor(o => o.UpdatedAt, (f, o) => o.CreatedAt.AddHours(f.Random.Int(0, 48)));
    }

    public IEnumerable<Order> Generate(int count) => _faker.Generate(count);
}
