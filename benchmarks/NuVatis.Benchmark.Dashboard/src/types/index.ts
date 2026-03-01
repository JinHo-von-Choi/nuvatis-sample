export interface BenchmarkResult {
  scenarioId: string;
  category: string;
  description: string;
  orm: 'NuVatis' | 'Dapper' | 'EfCore';
  meanMs: number;
  medianMs: number;
  p95Ms: number;
  p99Ms: number;
  memoryMB: number;
  gen0: number;
  gen1: number;
  gen2: number;
  throughput: number; // ops/sec
  allocatedBytesPerOp: number; // 작업당 할당 메모리
  cpuTimeMs: number; // CPU 시간
  consistency: number; // 일관성 (표준편차)
}

export interface CategorySummary {
  category: string;
  totalScenarios: number;
  nuvatisWins: number;
  dapperWins: number;
  efcoreWins: number;
  avgLatencyNuvatis: number;
  avgLatencyDapper: number;
  avgLatencyEfCore: number;
}

export interface OrmMetrics {
  orm: string;
  avgLatency: number;
  avgThroughput: number;
  avgMemory: number;
  totalGC: number;
}
