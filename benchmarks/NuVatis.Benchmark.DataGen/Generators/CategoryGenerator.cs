using Bogus;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.DataGen.Generators;

public class CategoryGenerator
{
    private readonly Faker<Category> _faker;

    public CategoryGenerator(int seed = 42)
    {
        var categories = new[] { "Electronics", "Clothing", "Home", "Books", "Toys", "Sports", "Beauty", "Food" };

        _faker = new Faker<Category>()
            .UseSeed(seed)
            .RuleFor(c => c.CategoryName, f => f.PickRandom(categories))
            .RuleFor(c => c.Description, f => f.Lorem.Sentence())
            .RuleFor(c => c.DisplayOrder, f => f.Random.Int(0, 100))
            .RuleFor(c => c.IsActive, f => f.Random.Bool(0.95f))
            .RuleFor(c => c.CreatedAt, f => f.Date.Between(DateTime.Now.AddYears(-2), DateTime.Now))
            .RuleFor(c => c.UpdatedAt, (f, c) => c.CreatedAt);
    }

    public IEnumerable<Category> Generate(int count) => _faker.Generate(count);

    public IEnumerable<Category> GenerateHierarchical(int rootCount, int maxDepth)
    {
        var categories = new List<Category>();
        var currentId = 1L;

        // 루트 카테고리 생성
        for (int i = 0; i < rootCount; i++)
        {
            var root = _faker.Generate();
            root.Id = currentId++;
            root.ParentId = null;
            categories.Add(root);

            // 하위 카테고리 생성 (재귀)
            if (maxDepth > 1)
            {
                GenerateChildren(categories, root.Id, 1, maxDepth, ref currentId);
            }
        }

        return categories;
    }

    private void GenerateChildren(List<Category> categories, long parentId, int currentDepth, int maxDepth, ref long currentId)
    {
        if (currentDepth >= maxDepth) return;

        var childCount = new Random().Next(2, 5); // 각 카테고리당 2-4개 자식
        for (int i = 0; i < childCount; i++)
        {
            var child = _faker.Generate();
            child.Id = currentId++;
            child.ParentId = parentId;
            categories.Add(child);

            GenerateChildren(categories, child.Id, currentDepth + 1, maxDepth, ref currentId);
        }
    }
}
