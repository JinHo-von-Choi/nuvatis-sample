using BenchmarkDotNet.Attributes;
using NuVatis.Benchmark.Core.Interfaces;

namespace NuVatis.Benchmark.Runner.Benchmarks;

/**
 * Complex 벤치마크 (5개 시나리오)
 * 목표: <100ms
 */
[MemoryDiagnoser]
[MarkdownExporter]
public class ComplexQueryBenchmarks
{
    private IOrderRepository    _nuvatis = null!;
    private IOrderRepository    _dapper  = null!;
    private IOrderRepository    _efcore  = null!;
    private ICategoryRepository _nuvatisC = null!;
    private ICategoryRepository _dapperC = null!;
    private ICategoryRepository _efcoreC = null!;

    [GlobalSetup]
    public void Setup()
    {
        // DI 컨테이너에서 Repository 주입
    }

    // Complex 1: FivePlusJoin
    [Benchmark(Baseline = true, Description = "FivePlusJoin - NuVatis")]
    public async Task NuVatis_FivePlusJoin()
        => await _nuvatis.GetCompleteOrderAsync(123456);

    [Benchmark(Description = "FivePlusJoin - Dapper")]
    public async Task Dapper_FivePlusJoin()
        => await _dapper.GetCompleteOrderAsync(123456);

    [Benchmark(Description = "FivePlusJoin - EF Core")]
    public async Task EfCore_FivePlusJoin()
        => await _efcore.GetCompleteOrderAsync(123456);

    // Complex 2: RecursiveCTE
    [Benchmark(Description = "RecursiveCTE - NuVatis")]
    public async Task NuVatis_RecursiveCTE()
        => await _nuvatisC.GetCategoryTreeAsync(1);

    [Benchmark(Description = "RecursiveCTE - Dapper")]
    public async Task Dapper_RecursiveCTE()
        => await _dapperC.GetCategoryTreeAsync(1);

    [Benchmark(Description = "RecursiveCTE - EF Core")]
    public async Task EfCore_RecursiveCTE()
        => await _efcoreC.GetCategoryTreeAsync(1);

    // Complex 3: NPlusOneProblem (잘못된 방식)
    [Benchmark(Description = "NPlusOne_Bad - NuVatis")]
    public async Task NuVatis_NPlusOne_Bad()
        => await _nuvatis.GetOrdersWithNPlusOneProblemAsync(123, 10);

    [Benchmark(Description = "NPlusOne_Bad - Dapper")]
    public async Task Dapper_NPlusOne_Bad()
        => await _dapper.GetOrdersWithNPlusOneProblemAsync(123, 10);

    [Benchmark(Description = "NPlusOne_Bad - EF Core")]
    public async Task EfCore_NPlusOne_Bad()
        => await _efcore.GetOrdersWithNPlusOneProblemAsync(123, 10);

    // Complex 4: NPlusOneProblem (최적화 방식)
    [Benchmark(Description = "NPlusOne_Optimized - NuVatis")]
    public async Task NuVatis_NPlusOne_Optimized()
        => await _nuvatis.GetOrdersOptimizedAsync(123, 10);

    [Benchmark(Description = "NPlusOne_Optimized - Dapper")]
    public async Task Dapper_NPlusOne_Optimized()
        => await _dapper.GetOrdersOptimizedAsync(123, 10);

    [Benchmark(Description = "NPlusOne_Optimized - EF Core")]
    public async Task EfCore_NPlusOne_Optimized()
        => await _efcore.GetOrdersOptimizedAsync(123, 10);

    // Complex 5: KeysetPaging
    [Benchmark(Description = "KeysetPaging - NuVatis")]
    public async Task NuVatis_KeysetPaging()
        => await _nuvatis.GetKeysetPagedAsync(1000000, 100);

    [Benchmark(Description = "KeysetPaging - Dapper")]
    public async Task Dapper_KeysetPaging()
        => await _dapper.GetKeysetPagedAsync(1000000, 100);

    [Benchmark(Description = "KeysetPaging - EF Core")]
    public async Task EfCore_KeysetPaging()
        => await _efcore.GetKeysetPagedAsync(1000000, 100);
}
