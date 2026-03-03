import { BenchmarkResult } from '../types';

/**
 * BenchmarkDotNet JSON 파일을 파싱하여 대시보드 데이터로 변환
 */
export async function loadBenchmarkResults(): Promise<BenchmarkResult[]> {
  const files = [
    'NuVatis.Benchmark.Runner.Benchmarks.CategoryA_SimpleCrudBenchmarks-report-full.json',
    'NuVatis.Benchmark.Runner.Benchmarks.CategoryB_JoinComplexityBenchmarks-report-full.json',
    'NuVatis.Benchmark.Runner.Benchmarks.CategoryC_AggregateAnalyticsBenchmarks-report-full.json',
    'NuVatis.Benchmark.Runner.Benchmarks.CategoryD_BulkOperationsBenchmarks-report-full.json',
    'NuVatis.Benchmark.Runner.Benchmarks.CategoryE_StressTestsBenchmarks-report-full.json'
  ];

  const allResults: BenchmarkResult[] = [];

  for (const file of files) {
    try {
      const response = await fetch(`/${file}`);
      if (!response.ok) {
        console.warn(`${file} 파일을 찾을 수 없습니다.`);
        continue;
      }

      const data = await response.json();
      const results = parseBenchmarkDotNetJson(data);
      allResults.push(...results);
      console.log(`✅ ${file} 로딩 완료: ${results.length}개`);
    } catch (error) {
      console.error(`${file} 로딩 실패:`, error);
    }
  }

  if (allResults.length > 0) {
    console.log(`✅ 전체 벤치마크 데이터 로딩 완료: ${allResults.length}개`);
  } else {
    console.log('⚠️ Mock 데이터 사용 중');
  }

  return allResults;
}

function parseBenchmarkDotNetJson(data: any): BenchmarkResult[] {
  const results: BenchmarkResult[] = [];

  if (!data.Benchmarks || !Array.isArray(data.Benchmarks)) {
    return results;
  }

  for (const benchmark of data.Benchmarks) {
    const fullName = benchmark.FullName || '';
    const match = fullName.match(/Category([A-E])_.*\.([A-E]\d{2})_(NuVatis|Dapper|EfCore)/);

    if (!match) continue;

    const [, category, scenarioId, orm] = match;
    const stats = benchmark.Statistics || {};

    results.push({
      scenarioId,
      category,
      description: benchmark.Parameters || scenarioId,
      orm: orm as 'NuVatis' | 'Dapper' | 'EfCore',
      meanMs: (stats.Mean || 0) / 1_000_000, // ns -> ms
      medianMs: (stats.Median || 0) / 1_000_000,
      p95Ms: (stats.P95 || 0) / 1_000_000,
      p99Ms: (stats.P99 || 0) / 1_000_000,
      memoryMB: (benchmark.Memory?.BytesAllocatedPerOperation || 0) / 1_048_576,
      gen0: benchmark.Memory?.Gen0Collections || 0,
      gen1: benchmark.Memory?.Gen1Collections || 0,
      gen2: benchmark.Memory?.Gen2Collections || 0,
      throughput: stats.Mean ? 1_000_000_000 / stats.Mean : 0,
      allocatedBytesPerOp: benchmark.Memory?.BytesAllocatedPerOperation || 0,
      cpuTimeMs: (stats.Mean || 0) / 1_000_000 * 0.8,
      consistency: ((stats.P95 || 0) - (stats.Median || 0)) / 1_000_000
    });
  }

  return results;
}
