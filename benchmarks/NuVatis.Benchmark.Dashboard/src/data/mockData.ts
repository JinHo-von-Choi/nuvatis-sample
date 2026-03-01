import { BenchmarkResult } from '../types';

// 60개 시나리오의 mock 데이터 생성
export const mockBenchmarkData: BenchmarkResult[] = [];

const categories = {
  A: { name: 'Simple CRUD', count: 15 },
  B: { name: 'JOIN Complexity', count: 15 },
  C: { name: 'Aggregate & Analytics', count: 15 },
  D: { name: 'Bulk Operations', count: 10 },
  E: { name: 'Stress Tests', count: 5 }
};

// 시나리오 ID별 설명
const scenarioDescriptions: Record<string, string> = {
  A01: 'PK 단일 조회',
  A02: 'PK 10회 반복 조회',
  A03: 'PK 100회 반복 조회',
  A04: 'PK 1K회 반복 조회',
  A05: 'WHERE 조건 조회 (10 rows)',
  A06: 'WHERE 조건 조회 (100 rows)',
  A07: 'WHERE 조건 조회 (1K rows)',
  A08: 'LIKE 검색 (100 rows)',
  A09: 'ORDER BY + LIMIT 10',
  A10: 'ORDER BY + LIMIT 100',
  A11: '단건 INSERT',
  A12: '100회 반복 INSERT',
  A13: '단건 UPDATE',
  A14: '100회 반복 UPDATE',
  A15: '단건 DELETE',

  B01: '2-table INNER JOIN',
  B02: '2-table LEFT JOIN',
  B03: '3-table JOIN (users-orders-items)',
  B04: '3-table JOIN 10회 반복',
  B05: '3-table JOIN 100회 반복',
  B06: '4-table JOIN (user-order-product-review)',
  B07: '5-table JOIN (+ payment, shipment)',
  B08: '6-table JOIN (+ address, category)',
  B09: '10-table JOIN',
  B10: '15-table FULL JOIN',
  B11: 'Self-JOIN (categories 계층)',
  B12: 'Multiple JOIN + WHERE',
  B13: 'Multiple JOIN + ORDER BY',
  B14: 'N+1 Problem 시뮬레이션 (100 orders)',
  B15: 'N+1 Problem 시뮬레이션 (1K orders)',

  C01: 'COUNT(*) 전체 테이블',
  C02: 'COUNT(*) + WHERE',
  C03: 'GROUP BY 단일 컬럼',
  C04: 'GROUP BY 2개 컬럼',
  C05: 'GROUP BY + COUNT + AVG + SUM',
  C06: 'GROUP BY + HAVING',
  C07: 'JOIN + GROUP BY',
  C08: 'JOIN + GROUP BY + ORDER BY',
  C09: 'Window Functions (ROW_NUMBER)',
  C10: 'Window Functions (RANK)',
  C11: 'Window Functions (PARTITION BY)',
  C12: 'Multiple Window Functions',
  C13: 'CTE (Common Table Expression)',
  C14: 'Recursive CTE (categories 트리)',
  C15: 'Multiple CTEs + JOIN',

  D01: 'BULK INSERT 100 rows',
  D02: 'BULK INSERT 1K rows',
  D03: 'BULK INSERT 10K rows',
  D04: 'BULK INSERT 100K rows',
  D05: 'BULK UPDATE 1K rows',
  D06: 'BULK UPDATE 10K rows',
  D07: 'BULK DELETE 1K rows',
  D08: 'Transaction (User + Address)',
  D09: 'Transaction (Order + Items x10)',
  D10: 'Complex Transaction (5 tables)',

  E01: '대량 조회 100K rows',
  E02: '복잡 쿼리 1K회 반복',
  E03: '전체 테이블 SCAN',
  E04: '동시성 시뮬레이션 (100 connections)',
  E05: '메모리 압박 테스트 (Large Result)'
};

// 카테고리별로 mock 데이터 생성
Object.entries(categories).forEach(([category, { count }]) => {
  for (let i = 1; i <= count; i++) {
    const scenarioId = `${category}${String(i).padStart(2, '0')}`;
    const description = scenarioDescriptions[scenarioId] || `Scenario ${scenarioId}`;

    // NuVatis 성능 (baseline)
    const baseLatency = getBaseLatency(category, i);

    ['NuVatis', 'Dapper', 'EfCore'].forEach(orm => {
      const multiplier = getOrmMultiplier(orm as any, category);
      const variance = 0.9 + Math.random() * 0.2; // ±10% 변동

      const mean = baseLatency * multiplier * variance;
      const median = mean * 0.95;
      const p95 = mean * 1.5;

      mockBenchmarkData.push({
        scenarioId,
        category,
        description,
        orm: orm as any,
        meanMs: mean,
        medianMs: median,
        p95Ms: p95,
        p99Ms: mean * 2.0,
        memoryMB: getMemoryUsage(orm as any, category, i),
        gen0: Math.floor(Math.random() * 10),
        gen1: Math.floor(Math.random() * 3),
        gen2: Math.floor(Math.random() * 2),
        throughput: 1000 / mean,
        allocatedBytesPerOp: getAllocatedBytes(orm as any, category),
        cpuTimeMs: mean * 0.8, // CPU 시간은 응답 시간의 80% 정도
        consistency: p95 - median // 일관성 (표준편차 근사)
      });
    });
  }
});

// Helper functions
function getBaseLatency(category: string, index: number): number {
  const base = {
    A: 0.5 + index * 0.1,
    B: 2 + index * 0.5,
    C: 5 + index * 1,
    D: 10 + index * 5,
    E: 50 + index * 20
  };
  return base[category as keyof typeof base] || 1;
}

function getOrmMultiplier(orm: 'NuVatis' | 'Dapper' | 'EfCore', category: string): number {
  const multipliers = {
    A: { NuVatis: 1.0, Dapper: 0.95, EfCore: 1.15 },
    B: { NuVatis: 0.95, Dapper: 1.0, EfCore: 1.3 },
    C: { NuVatis: 0.9, Dapper: 1.05, EfCore: 1.4 },
    D: { NuVatis: 1.0, Dapper: 0.95, EfCore: 2.0 },
    E: { NuVatis: 1.0, Dapper: 0.98, EfCore: 1.8 }
  };
  return multipliers[category as keyof typeof multipliers]?.[orm] || 1.0;
}

function getMemoryUsage(orm: 'NuVatis' | 'Dapper' | 'EfCore', category: string, index: number): number {
  const base = {
    A: 2 + index * 0.5,
    B: 5 + index * 1,
    C: 10 + index * 2,
    D: 20 + index * 5,
    E: 100 + index * 50
  };

  const memMultipliers = {
    NuVatis: 1.1,
    Dapper: 1.0,
    EfCore: 1.5
  };

  return (base[category as keyof typeof base] || 5) * memMultipliers[orm];
}

function getAllocatedBytes(orm: 'NuVatis' | 'Dapper' | 'EfCore', category: string): number {
  const baseAllocation = {
    A: 1024,      // 1 KB for simple queries
    B: 4096,      // 4 KB for joins
    C: 8192,      // 8 KB for aggregates
    D: 102400,    // 100 KB for bulk
    E: 1048576    // 1 MB for stress
  };

  const allocMultipliers = {
    NuVatis: 1.2,  // XML 파싱 오버헤드
    Dapper: 1.0,   // 최소 할당
    EfCore: 2.5    // Change Tracking + Proxy
  };

  return (baseAllocation[category as keyof typeof baseAllocation] || 1024) * allocMultipliers[orm];
}
