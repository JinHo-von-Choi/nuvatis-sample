using BenchmarkDotNet.Attributes;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Runner.Benchmarks;

/**
 * Simple 벤치마크 (5개 시나리오)
 * 목표: <5ms
 */
[MemoryDiagnoser]
[MarkdownExporter]
public class SimpleCrudBenchmarks
{
    private IUserRepository   _nuvatis  = null!;
    private IUserRepository   _dapper   = null!;
    private IUserRepository   _efcore   = null!;
    private IProductRepository _nuvatisProd = null!;
    private IProductRepository _dapperProd = null!;
    private IProductRepository _efcoreProd = null!;

    [GlobalSetup]
    public void Setup()
    {
        // DI 컨테이너에서 Repository 주입
        // 실제 구현 시 ServiceProvider 사용
    }

    // Simple 1: GetById
    [Benchmark(Baseline = true, Description = "GetById - NuVatis")]
    public async Task<User?> NuVatis_GetById()
        => await _nuvatis.GetByIdAsync(12345);

    [Benchmark(Description = "GetById - Dapper")]
    public async Task<User?> Dapper_GetById()
        => await _dapper.GetByIdAsync(12345);

    [Benchmark(Description = "GetById - EF Core")]
    public async Task<User?> EfCore_GetById()
        => await _efcore.GetByIdAsync(12345);

    // Simple 2: WhereClause
    [Benchmark(Description = "WhereClause - NuVatis")]
    public async Task<IEnumerable<User>> NuVatis_WhereClause()
        => await _nuvatis.GetByEmailDomainAsync("gmail.com");

    [Benchmark(Description = "WhereClause - Dapper")]
    public async Task<IEnumerable<User>> Dapper_WhereClause()
        => await _dapper.GetByEmailDomainAsync("gmail.com");

    [Benchmark(Description = "WhereClause - EF Core")]
    public async Task<IEnumerable<User>> EfCore_WhereClause()
        => await _efcore.GetByEmailDomainAsync("gmail.com");

    // Simple 3: SimplePaging
    [Benchmark(Description = "SimplePaging - NuVatis")]
    public async Task<IEnumerable<User>> NuVatis_SimplePaging()
        => await _nuvatis.GetPagedAsync(0, 10);

    [Benchmark(Description = "SimplePaging - Dapper")]
    public async Task<IEnumerable<User>> Dapper_SimplePaging()
        => await _dapper.GetPagedAsync(0, 10);

    [Benchmark(Description = "SimplePaging - EF Core")]
    public async Task<IEnumerable<User>> EfCore_SimplePaging()
        => await _efcore.GetPagedAsync(0, 10);

    // Simple 4: InsertSingle
    [Benchmark(Description = "InsertSingle - NuVatis")]
    public async Task<long> NuVatis_InsertSingle()
        => await _nuvatis.InsertAsync(CreateDummyUser());

    [Benchmark(Description = "InsertSingle - Dapper")]
    public async Task<long> Dapper_InsertSingle()
        => await _dapper.InsertAsync(CreateDummyUser());

    [Benchmark(Description = "InsertSingle - EF Core")]
    public async Task<long> EfCore_InsertSingle()
        => await _efcore.InsertAsync(CreateDummyUser());

    // Simple 5: UpdateSingle
    [Benchmark(Description = "UpdateSingle - NuVatis")]
    public async Task<int> NuVatis_UpdateSingle()
        => await _nuvatisProd.UpdateStockAsync(12345, 100);

    [Benchmark(Description = "UpdateSingle - Dapper")]
    public async Task<int> Dapper_UpdateSingle()
        => await _dapperProd.UpdateStockAsync(12345, 100);

    [Benchmark(Description = "UpdateSingle - EF Core")]
    public async Task<int> EfCore_UpdateSingle()
        => await _efcoreProd.UpdateStockAsync(12345, 100);

    private User CreateDummyUser() => new()
    {
        UserName     = "benchmark_user",
        Email        = "benchmark@test.com",
        FullName     = "Benchmark User",
        PasswordHash = "hashed_password",
        IsActive     = true,
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow
    };
}
