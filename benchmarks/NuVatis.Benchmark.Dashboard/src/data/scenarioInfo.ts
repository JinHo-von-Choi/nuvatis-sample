/**
 * 벤치마크 카테고리 및 시나리오 상세 정보
 */

export interface CategoryInfo {
  name: string;
  description: string;
  icon: string;
  color: string;
  focus: string;
  examples: string[];
}

export interface ScenarioInfo {
  id: string;
  name: string;
  description: string;
  complexity: string;
  realWorldUse: string;
  expectedWinner: string;
  whyItMatters: string;
}

export const categoryInfo: Record<string, CategoryInfo> = {
  A: {
    name: 'Simple CRUD',
    description: '기본적인 CRUD 작업과 단순 쿼리',
    icon: '📝',
    color: 'bg-blue-500',
    focus: '단일 테이블 조회, 기본 INSERT/UPDATE/DELETE',
    examples: [
      'PK로 단건 조회 (WHERE id = ?)',
      '페이징 조회 (OFFSET/LIMIT)',
      'LIKE 검색',
      '단건 INSERT/UPDATE'
    ]
  },
  B: {
    name: 'JOIN Complexity',
    description: '다중 테이블 조인과 관계 탐색',
    icon: '🔗',
    color: 'bg-green-500',
    focus: '2~5개 테이블 JOIN, N+1 Problem',
    examples: [
      'User + Address (2-table JOIN)',
      'Order + Items + Product (3-table JOIN)',
      'N+1 Problem 시뮬레이션',
      '반복 조인 쿼리'
    ]
  },
  C: {
    name: 'Aggregate & Analytics',
    description: '집계 함수와 분석 쿼리',
    icon: '📊',
    color: 'bg-purple-500',
    focus: 'GROUP BY, Window Functions, CTE',
    examples: [
      'GROUP BY + AVG/SUM/COUNT',
      'Window Functions (ROW_NUMBER, RANK)',
      'CTE (Common Table Expression)',
      'Recursive CTE (계층 구조)'
    ]
  },
  D: {
    name: 'Bulk Operations',
    description: '대량 데이터 처리와 트랜잭션',
    icon: '⚡',
    color: 'bg-amber-500',
    focus: 'BULK INSERT, Transaction',
    examples: [
      'BULK INSERT 1K~100K rows',
      '트랜잭션 (다중 테이블)',
      'BULK UPDATE/DELETE',
      'PostgreSQL COPY 프로토콜'
    ]
  },
  E: {
    name: 'Stress Tests',
    description: '부하 테스트와 한계 측정',
    icon: '🔥',
    color: 'bg-red-500',
    focus: '대량 조회, 동시성, 메모리 압박',
    examples: [
      '10K~100K rows 조회',
      '복잡 쿼리 100회 반복',
      '동시성 50~100 connections',
      '메모리 압박 테스트'
    ]
  }
};

export const scenarioDetails: Record<string, ScenarioInfo> = {
  // Category A: Simple CRUD
  A01: {
    id: 'A01',
    name: 'PK 단일 조회',
    description: 'Primary Key로 단건 조회하는 가장 기본적인 쿼리',
    complexity: 'Simple',
    realWorldUse: '사용자 프로필 조회, 상품 상세 페이지',
    expectedWinner: 'Dapper',
    whyItMatters: '가장 빈번한 쿼리 패턴으로 기본 성능을 측정'
  },
  A02: {
    id: 'A02',
    name: 'PK 10회 반복 조회',
    description: 'PK 조회를 10회 반복하여 연속 호출 성능 측정',
    complexity: 'Simple',
    realWorldUse: '관련 아이템 조회, 추천 시스템',
    expectedWinner: 'Dapper',
    whyItMatters: '커넥션 풀 효율성과 반복 쿼리 오버헤드 측정'
  },
  A03: {
    id: 'A03',
    name: 'PK 100회 반복 조회',
    description: 'PK 조회를 100회 반복하여 중급 부하 성능 측정',
    complexity: 'Medium',
    realWorldUse: '배치 처리, 대시보드 데이터 로딩',
    expectedWinner: 'Dapper',
    whyItMatters: '반복 쿼리에서 ORM 오버헤드가 누적되는 정도 측정'
  },
  A05: {
    id: 'A05',
    name: 'WHERE 조건 조회 (10 rows)',
    description: 'WHERE 절로 소량 데이터 필터링',
    complexity: 'Simple',
    realWorldUse: '최근 알림 조회, 오늘의 주문 목록',
    expectedWinner: 'Dapper',
    whyItMatters: '조건절 성능과 소량 결과 매핑 효율성'
  },
  A08: {
    id: 'A08',
    name: 'LIKE 검색 (100 rows)',
    description: 'LIKE 패턴 매칭으로 검색',
    complexity: 'Medium',
    realWorldUse: '사용자명 검색, 상품명 자동완성',
    expectedWinner: 'Dapper',
    whyItMatters: '전체 스캔이 필요한 검색 쿼리의 효율성'
  },

  // Category B: JOIN Complexity
  B01: {
    id: 'B01',
    name: '2-table JOIN',
    description: 'User와 Address를 조인하여 조회',
    complexity: 'Medium',
    realWorldUse: '사용자 + 배송지 정보 조회',
    expectedWinner: 'Dapper/NuVatis',
    whyItMatters: '가장 기본적인 관계 탐색 패턴'
  },
  B03: {
    id: 'B03',
    name: '3-table JOIN',
    description: 'Order + Items + Product 조인',
    complexity: 'Medium',
    realWorldUse: '주문 상세 페이지 (상품 정보 포함)',
    expectedWinner: 'NuVatis',
    whyItMatters: '복잡한 관계를 한 번에 가져오는 효율성'
  },
  B06: {
    id: 'B06',
    name: 'JOIN 10회 반복',
    description: '3-table JOIN을 10회 반복 실행',
    complexity: 'Complex',
    realWorldUse: '주문 목록 페이지 (각 주문의 상세 정보)',
    expectedWinner: 'NuVatis',
    whyItMatters: '반복 조인에서 매핑 오버헤드 측정'
  },
  B11: {
    id: 'B11',
    name: '복합 쿼리',
    description: '사용자별 주문 목록 조회',
    complexity: 'Medium',
    realWorldUse: '마이페이지 주문 내역',
    expectedWinner: 'NuVatis/Dapper',
    whyItMatters: 'WHERE + JOIN 조합의 실전 패턴'
  },

  // Category C: Aggregate & Analytics
  C01: {
    id: 'C01',
    name: 'Aggregate AVG',
    description: '상품별 평균 평점 계산 (GROUP BY + AVG)',
    complexity: 'Medium',
    realWorldUse: '상품 평점 집계, 통계 대시보드',
    expectedWinner: 'Dapper',
    whyItMatters: '집계 함수 성능과 그룹핑 효율성'
  },
  C06: {
    id: 'C06',
    name: 'Window Functions',
    description: '상품별 Top N 리뷰 추출 (ROW_NUMBER)',
    complexity: 'Complex',
    realWorldUse: '베스트 리뷰 선정, 랭킹 시스템',
    expectedWinner: 'NuVatis',
    whyItMatters: '고급 SQL 기능의 ORM 지원 수준'
  },
  C11: {
    id: 'C11',
    name: 'JOIN + GROUP BY',
    description: '국가별 사용자 수 집계 (User + Address)',
    complexity: 'Complex',
    realWorldUse: '지역별 통계, 국가별 매출 분석',
    expectedWinner: 'NuVatis',
    whyItMatters: 'JOIN과 집계의 조합 효율성'
  },

  // Category D: Bulk Operations
  D01: {
    id: 'D01',
    name: 'BULK INSERT 1K',
    description: '1,000건 대량 삽입 (PostgreSQL COPY)',
    complexity: 'Complex',
    realWorldUse: '데이터 마이그레이션, 배치 처리',
    expectedWinner: 'Dapper',
    whyItMatters: 'COPY 프로토콜 활용 효율성'
  },
  D05: {
    id: 'D05',
    name: 'Transaction',
    description: '트랜잭션 내 다중 테이블 작업 (User + Address)',
    complexity: 'Medium',
    realWorldUse: '회원가입, 주문 생성',
    expectedWinner: 'EF Core',
    whyItMatters: '트랜잭션 관리와 일관성 보장'
  },

  // Category E: Stress Tests
  E01: {
    id: 'E01',
    name: '대량 조회 10K',
    description: '10,000건 조회 (메모리/GC 압박)',
    complexity: 'Stress',
    realWorldUse: '대용량 엑셀 다운로드, 전체 데이터 내보내기',
    expectedWinner: 'Dapper',
    whyItMatters: '대량 데이터 처리 시 메모리 효율성'
  },
  E02: {
    id: 'E02',
    name: '복잡 쿼리 100회',
    description: '복잡한 JOIN 쿼리를 100회 반복',
    complexity: 'Stress',
    realWorldUse: '고부하 상황, 많은 사용자 동시 접속',
    expectedWinner: 'NuVatis',
    whyItMatters: '반복 쿼리에서 성능 저하 정도'
  },
  E03: {
    id: 'E03',
    name: '동시성 50',
    description: '50개 병렬 요청 (동시성 테스트)',
    complexity: 'Stress',
    realWorldUse: '다중 사용자 동시 접속',
    expectedWinner: 'Dapper',
    whyItMatters: '커넥션 풀과 동시성 처리 효율성'
  }
};

export const performanceMetrics = [
  {
    id: 'latency',
    name: 'Latency (응답 시간)',
    unit: 'ms',
    description: '쿼리 실행 시작부터 결과 반환까지 걸리는 시간',
    lowerIsBetter: true,
    icon: '⏱️',
    importance: 'critical',
    why: '사용자 경험에 직접적인 영향. 100ms 이하가 이상적'
  },
  {
    id: 'throughput',
    name: 'Throughput (처리량)',
    unit: 'ops/sec',
    description: '초당 처리 가능한 작업 수',
    lowerIsBetter: false,
    icon: '🚀',
    importance: 'high',
    why: '시스템 용량과 확장성을 나타냄. 높을수록 더 많은 부하 처리 가능'
  },
  {
    id: 'memory',
    name: 'Memory (메모리 사용량)',
    unit: 'MB',
    description: '작업 수행 시 할당된 총 메모리',
    lowerIsBetter: true,
    icon: '💾',
    importance: 'high',
    why: '메모리 효율성. 낮을수록 더 많은 동시 요청 처리 가능'
  },
  {
    id: 'gc',
    name: 'GC Pressure (GC 압박)',
    unit: 'collections',
    description: 'Garbage Collection 발생 횟수 (Gen0/1/2)',
    lowerIsBetter: true,
    icon: '🗑️',
    importance: 'medium',
    why: 'GC가 빈번하면 응답 시간 증가. Gen2 GC는 특히 비용이 큼'
  },
  {
    id: 'allocatedPerOp',
    name: 'Allocated/Op (작업당 할당)',
    unit: 'bytes',
    description: '작업 1회당 할당된 메모리',
    lowerIsBetter: true,
    icon: '📦',
    importance: 'medium',
    why: '메모리 할당 효율성. 낮을수록 GC 압박 감소'
  },
  {
    id: 'consistency',
    name: 'Consistency (일관성)',
    unit: 'σ (stddev)',
    description: '응답 시간의 표준편차 (P95 - P50)',
    lowerIsBetter: true,
    icon: '📏',
    importance: 'medium',
    why: '예측 가능성. 낮을수록 안정적인 성능'
  },
  {
    id: 'scalability',
    name: 'Scalability (확장성)',
    unit: 'ratio',
    description: '부하 증가 시 성능 저하 비율',
    lowerIsBetter: true,
    icon: '📈',
    importance: 'high',
    why: '트래픽 증가 시 성능 유지 능력'
  },
  {
    id: 'cpuTime',
    name: 'CPU Time (CPU 시간)',
    unit: 'ms',
    description: 'CPU 사용 시간 (User + System)',
    lowerIsBetter: true,
    icon: '⚙️',
    importance: 'medium',
    why: 'CPU 효율성. 낮을수록 더 적은 리소스로 처리'
  }
];

export const ormCharacteristics = {
  NuVatis: {
    strengths: [
      '복잡한 ResultMap 매핑 (MyBatis 스타일)',
      '동적 SQL 생성 (조건부 WHERE/JOIN)',
      'XML 기반 쿼리 관리 (재사용성)',
      '복잡한 JOIN 최적화'
    ],
    weaknesses: [
      'XML 파싱 오버헤드',
      '컴파일 타임 체크 부재',
      '단순 쿼리에서 Dapper보다 느림',
      '학습 곡선 (XML 문법)'
    ],
    bestFor: [
      '복잡한 도메인 모델',
      '동적 쿼리가 많은 시스템',
      '레거시 DB 통합',
      'MyBatis 경험자'
    ]
  },
  Dapper: {
    strengths: [
      '가장 빠른 매핑 속도',
      '최소 메모리 사용량',
      '낮은 GC 압박',
      'COPY 프로토콜 직접 활용 가능'
    ],
    weaknesses: [
      '동적 SQL 수동 구성',
      'N+1 문제 수동 해결',
      'SQL 문자열 관리 어려움',
      'Change Tracking 없음'
    ],
    bestFor: [
      '고성능이 중요한 시스템',
      '단순하고 명확한 쿼리',
      '마이크로서비스',
      'SQL 숙련자'
    ]
  },
  EfCore: {
    strengths: [
      'LINQ 타입 안전성',
      'Change Tracking (자동 업데이트)',
      '마이그레이션 도구',
      '생산성 (코드 퍼스트)'
    ],
    weaknesses: [
      '복잡 쿼리 비효율',
      '높은 메모리 사용량',
      'Bulk 작업 매우 느림',
      'Change Tracking 오버헤드'
    ],
    bestFor: [
      'CRUD 중심 애플리케이션',
      '.NET 표준 준수',
      '빠른 프로토타이핑',
      'Entity 중심 설계'
    ]
  }
};
