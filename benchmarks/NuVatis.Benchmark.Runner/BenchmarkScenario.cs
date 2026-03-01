using BenchmarkDotNet.Attributes;

namespace NuVatis.Benchmark.Runner;

/**
 * 벤치마크 시나리오 메타데이터
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public record BenchmarkScenario(
    string Id,
    string Category,
    string Description,
    Complexity Complexity,
    Iterations Iterations,
    ResultSize ResultSize,
    TableJoins TableJoins,
    OperationType OperationType
);

public enum Complexity
{
    C1_Simple,
    C2_Medium,
    C3_Complex,
    C4_VeryComplex,
    C5_Extreme
}

public enum Iterations
{
    I1_Single,
    I2_10x,
    I3_100x,
    I4_1Kx,
    I5_10Kx,
    I6_100Kx
}

public enum ResultSize
{
    R1_Tiny,        // 1-10 rows
    R2_Small,       // 10-100 rows
    R3_Medium,      // 100-1K rows
    R4_Large,       // 1K-10K rows
    R5_Huge,        // 10K-100K rows
    R6_Massive      // 100K-1M rows
}

public enum TableJoins
{
    T1_Single,      // 1 table
    T2_Double,      // 2 tables
    T3_Triple,      // 3 tables
    T4_Quad,        // 4 tables
    T5_Penta,       // 5 tables
    T6_Multi,       // 6-10 tables
    T7_Full         // 11-15 tables
}

public enum OperationType
{
    O1_Read,
    O2_Insert,
    O3_Update,
    O4_Delete,
    O5_BulkInsert,
    O6_Transaction,
    O7_Aggregate,
    O8_Analytical
}

/**
 * 벤치마크 결과 DTO
 */
public class BenchmarkResult
{
    public string ScenarioId { get; set; } = string.Empty;
    public string OrmType { get; set; } = string.Empty;
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public long MemoryAllocatedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public DateTime ExecutedAt { get; set; }
}
