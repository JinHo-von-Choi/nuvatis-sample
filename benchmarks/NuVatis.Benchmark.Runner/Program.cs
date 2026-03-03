using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using NuVatis.Benchmark.Runner.Benchmarks;

/**
 * NuVatis 대규모 ORM 벤치마크 Runner
 * BenchmarkDotNet 기반
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║      NuVatis 대규모 ORM 벤치마크 (vs Dapper vs EF Core)      ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddColumn(StatisticColumn.P50, StatisticColumn.P95)
    .AddColumn(RankColumn.Arabic)
    .AddExporter(MarkdownExporter.GitHub)
    .AddExporter(HtmlExporter.Default)
    .AddExporter(BenchmarkDotNet.Exporters.Json.JsonExporter.Full)
    .AddJob(Job.Default
        // InProcessEmitToolchain 제거 - WDAC 충돌 방지 (OutOfProcess 기본 툴체인 사용)
        .WithWarmupCount(3)
        .WithIterationCount(10))
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

Console.WriteLine("실행할 벤치마크 카테고리:");
Console.WriteLine("  A. Simple CRUD (15개 시나리오)");
Console.WriteLine("  B. JOIN Complexity (15개 시나리오)");
Console.WriteLine("  C. Aggregate & Analytics (15개 시나리오)");
Console.WriteLine("  D. Bulk Operations (10개 시나리오)");
Console.WriteLine("  E. Stress Tests (5개 시나리오)");
Console.WriteLine("  0. 모두 실행 (60개 시나리오)");
Console.Write("\n선택: ");

var choice = Console.ReadLine()?.ToUpper();

switch (choice)
{
    case "A":
        Console.WriteLine("\n▶ Category A: Simple CRUD 벤치마크 실행 중...\n");
        BenchmarkRunner.Run<CategoryA_SimpleCrudBenchmarks>(config);
        break;

    case "B":
        Console.WriteLine("\n▶ Category B: JOIN Complexity 벤치마크 실행 중...\n");
        BenchmarkRunner.Run<CategoryB_JoinComplexityBenchmarks>(config);
        break;

    case "C":
        Console.WriteLine("\n▶ Category C: Aggregate & Analytics 벤치마크 실행 중...\n");
        BenchmarkRunner.Run<CategoryC_AggregateAnalyticsBenchmarks>(config);
        break;

    case "D":
        Console.WriteLine("\n▶ Category D: Bulk Operations 벤치마크 실행 중...\n");
        BenchmarkRunner.Run<CategoryD_BulkOperationsBenchmarks>(config);
        break;

    case "E":
        Console.WriteLine("\n▶ Category E: Stress Tests 벤치마크 실행 중...\n");
        BenchmarkRunner.Run<CategoryE_StressTestsBenchmarks>(config);
        break;

    case "0":
    default:
        Console.WriteLine("\n▶ 전체 벤치마크 실행 중 (60개 시나리오)...\n");
        BenchmarkRunner.Run<CategoryA_SimpleCrudBenchmarks>(config);
        BenchmarkRunner.Run<CategoryB_JoinComplexityBenchmarks>(config);
        BenchmarkRunner.Run<CategoryC_AggregateAnalyticsBenchmarks>(config);
        BenchmarkRunner.Run<CategoryD_BulkOperationsBenchmarks>(config);
        BenchmarkRunner.Run<CategoryE_StressTestsBenchmarks>(config);
        break;
}

Console.WriteLine("\n✓ 벤치마크 완료!");
Console.WriteLine("결과 파일: BenchmarkDotNet.Artifacts/results/");

// 대시보드 자동 업데이트
Console.WriteLine("\n📊 대시보드 업데이트 중...");
UpdateDashboard();

static void UpdateDashboard()
{
    try
    {
        var resultsDir = "BenchmarkDotNet.Artifacts/results";
        var dashboardPublicDir = "D:/jobs/nu/nuvatis-sample/benchmarks/NuVatis.Benchmark.Dashboard/public";

        if (!Directory.Exists(resultsDir))
        {
            Console.WriteLine($"⚠️ 결과 디렉토리를 찾을 수 없습니다: {resultsDir}");
            return;
        }

        if (!Directory.Exists(dashboardPublicDir))
        {
            Console.WriteLine($"⚠️ 대시보드 public 디렉토리를 찾을 수 없습니다: {dashboardPublicDir}");
            return;
        }

        var jsonFiles = Directory.GetFiles(resultsDir, "*-report-full.json");
        var copiedCount = 0;

        foreach (var sourceFile in jsonFiles)
        {
            var fileName = Path.GetFileName(sourceFile);
            var destFile = Path.Combine(dashboardPublicDir, fileName);
            File.Copy(sourceFile, destFile, overwrite: true);
            copiedCount++;
            Console.WriteLine($"  ✓ {fileName}");
        }

        if (copiedCount > 0)
        {
            Console.WriteLine($"\n✅ {copiedCount}개 파일 복사 완료!");
            Console.WriteLine("\n대시보드 실행:");
            Console.WriteLine("  cd D:/jobs/nu/nuvatis-sample/benchmarks/NuVatis.Benchmark.Dashboard");
            Console.WriteLine("  npm run dev");
        }
        else
        {
            Console.WriteLine("⚠️ 복사할 JSON 파일이 없습니다.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ 대시보드 업데이트 오류: {ex.Message}");
    }
}
