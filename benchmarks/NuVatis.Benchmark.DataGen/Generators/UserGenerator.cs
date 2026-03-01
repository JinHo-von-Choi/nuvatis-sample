using Bogus;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.DataGen.Generators;

/**
 * Bogus 기반 사용자 데이터 생성기
 * 재현 가능성을 위해 시드 고정 (42)
 */
public class UserGenerator
{
    private readonly Faker<User> _faker;

    public UserGenerator(int seed = 42)
    {
        _faker = new Faker<User>()
            .UseSeed(seed)
            .RuleFor(u => u.UserName, f => f.Internet.UserName())
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.FullName, f => f.Name.FullName())
            .RuleFor(u => u.PasswordHash, f => f.Internet.Password(20))
            .RuleFor(u => u.DateOfBirth, f => f.Date.Between(DateTime.Now.AddYears(-80), DateTime.Now.AddYears(-18)))
            .RuleFor(u => u.PhoneNumber, f => f.Phone.PhoneNumber())
            .RuleFor(u => u.IsActive, f => f.Random.Bool(0.95f)) // 95% 활성
            .RuleFor(u => u.CreatedAt, f => f.Date.Between(DateTime.Now.AddYears(-3), DateTime.Now))
            .RuleFor(u => u.UpdatedAt, (f, u) => u.CreatedAt.AddDays(f.Random.Int(0, 30)));
    }

    public IEnumerable<User> Generate(int count) => _faker.Generate(count);
}
