using Bogus;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.DataGen.Generators;

public class ProductGenerator
{
    private readonly Faker<Product> _faker;

    public ProductGenerator(List<long> categoryIds, int seed = 42)
    {
        _faker = new Faker<Product>()
            .UseSeed(seed)
            .RuleFor(p => p.CategoryId, f => f.PickRandom(categoryIds))
            .RuleFor(p => p.ProductName, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Price, f => decimal.Parse(f.Commerce.Price(10, 1000, 2)))
            .RuleFor(p => p.CostPrice, (f, p) => p.Price * 0.6m) // 40% 마진
            .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 1000))
            .RuleFor(p => p.Sku, f => f.Commerce.Ean13())
            .RuleFor(p => p.IsActive, f => f.Random.Bool(0.95f))
            .RuleFor(p => p.CreatedAt, f => f.Date.Between(DateTime.Now.AddYears(-2), DateTime.Now))
            .RuleFor(p => p.UpdatedAt, (f, p) => p.CreatedAt);
    }

    public IEnumerable<Product> Generate(int count) => _faker.Generate(count);
}
