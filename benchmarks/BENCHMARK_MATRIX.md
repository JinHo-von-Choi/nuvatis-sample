# NuVatis ORM 벤치마크 매트릭스

작성자: 최진호
작성일: 2026-03-01

## 벤치마크 차원 (5D Matrix)

### 차원 1: 쿼리 복잡도 (Complexity)
- **C1_Simple**: PK 단일 조회 (1 table, WHERE id = ?)
- **C2_Medium**: WHERE + JOIN 2-3개 테이블
- **C3_Complex**: JOIN 4-5개 + GROUP BY/HAVING
- **C4_VeryComplex**: JOIN 6+ + Window Functions + CTE
- **C5_Extreme**: Recursive CTE + Multiple Subqueries + UNION

### 차원 2: 반복 횟수 (Iterations)
- **I1_Single**: 1회 실행
- **I2_10x**: 10회 반복
- **I3_100x**: 100회 반복
- **I4_1Kx**: 1,000회 반복
- **I5_10Kx**: 10,000회 반복
- **I6_100Kx**: 100,000회 반복

### 차원 3: 결과 크기 (Result Size)
- **R1_Tiny**: 1-10 rows
- **R2_Small**: 10-100 rows
- **R3_Medium**: 100-1K rows
- **R4_Large**: 1K-10K rows
- **R5_Huge**: 10K-100K rows
- **R6_Massive**: 100K-1M rows

### 차원 4: 테이블 조합 (Table Joins)
- **T1_Single**: 1 table
- **T2_Double**: 2 tables JOIN
- **T3_Triple**: 3 tables JOIN
- **T4_Quad**: 4 tables JOIN
- **T5_Penta**: 5 tables JOIN
- **T6_Multi**: 6-10 tables JOIN
- **T7_Full**: 11-15 tables JOIN (전체 스키마)

### 차원 5: 작업 유형 (Operation Type)
- **O1_Read**: SELECT 조회
- **O2_Insert**: INSERT 단건
- **O3_Update**: UPDATE 단건
- **O4_Delete**: DELETE 단건
- **O5_BulkInsert**: BULK INSERT (1K-100K)
- **O6_Transaction**: 트랜잭션 (다중 작업)
- **O7_Aggregate**: 집계 쿼리 (COUNT, SUM, AVG, GROUP BY)
- **O8_Analytical**: 분석 쿼리 (Window Functions, RANK, ROW_NUMBER)

## 핵심 벤치마크 시나리오 (60개)

### 카테고리 A: Simple CRUD (15개)

| ID | 설명 | 복잡도 | 반복 | 결과 | 테이블 | 작업 |
|----|------|--------|------|------|--------|------|
| A01 | PK 단일 조회 | C1 | I1 | R1 | T1 | O1 |
| A02 | PK 10회 반복 조회 | C1 | I2 | R1 | T1 | O1 |
| A03 | PK 100회 반복 조회 | C1 | I3 | R1 | T1 | O1 |
| A04 | PK 1K회 반복 조회 | C1 | I4 | R1 | T1 | O1 |
| A05 | WHERE 조건 조회 (10 rows) | C1 | I1 | R1 | T1 | O1 |
| A06 | WHERE 조건 조회 (100 rows) | C1 | I1 | R2 | T1 | O1 |
| A07 | WHERE 조건 조회 (1K rows) | C1 | I1 | R3 | T1 | O1 |
| A08 | LIKE 검색 (100 rows) | C1 | I1 | R2 | T1 | O1 |
| A09 | ORDER BY + LIMIT 10 | C1 | I1 | R1 | T1 | O1 |
| A10 | ORDER BY + LIMIT 100 | C1 | I1 | R2 | T1 | O1 |
| A11 | 단건 INSERT | C1 | I1 | - | T1 | O2 |
| A12 | 100회 반복 INSERT | C1 | I3 | - | T1 | O2 |
| A13 | 단건 UPDATE | C1 | I1 | - | T1 | O3 |
| A14 | 100회 반복 UPDATE | C1 | I3 | - | T1 | O3 |
| A15 | 단건 DELETE | C1 | I1 | - | T1 | O4 |

### 카테고리 B: JOIN Complexity (15개)

| ID | 설명 | 복잡도 | 반복 | 결과 | 테이블 | 작업 |
|----|------|--------|------|------|--------|------|
| B01 | 2-table INNER JOIN | C2 | I1 | R2 | T2 | O1 |
| B02 | 2-table LEFT JOIN | C2 | I1 | R2 | T2 | O1 |
| B03 | 3-table JOIN (users-orders-items) | C2 | I1 | R3 | T3 | O1 |
| B04 | 3-table JOIN 10회 반복 | C2 | I2 | R3 | T3 | O1 |
| B05 | 3-table JOIN 100회 반복 | C2 | I3 | R3 | T3 | O1 |
| B06 | 4-table JOIN (user-order-product-review) | C3 | I1 | R3 | T4 | O1 |
| B07 | 5-table JOIN (+ payment, shipment) | C3 | I1 | R3 | T5 | O1 |
| B08 | 6-table JOIN (+ address, category) | C4 | I1 | R3 | T6 | O1 |
| B09 | 10-table JOIN | C4 | I1 | R4 | T6 | O1 |
| B10 | 15-table FULL JOIN | C5 | I1 | R5 | T7 | O1 |
| B11 | Self-JOIN (categories 계층) | C2 | I1 | R2 | T1 | O1 |
| B12 | Multiple JOIN + WHERE | C3 | I1 | R3 | T4 | O1 |
| B13 | Multiple JOIN + ORDER BY | C3 | I1 | R3 | T4 | O1 |
| B14 | N+1 Problem 시뮬레이션 (100 orders) | C2 | I3 | R3 | T2 | O1 |
| B15 | N+1 Problem 시뮬레이션 (1K orders) | C2 | I4 | R4 | T2 | O1 |

### 카테고리 C: Aggregate & Analytics (15개)

| ID | 설명 | 복잡도 | 반복 | 결과 | 테이블 | 작업 |
|----|------|--------|------|------|--------|------|
| C01 | COUNT(*) 전체 테이블 | C1 | I1 | R1 | T1 | O7 |
| C02 | COUNT(*) + WHERE | C1 | I1 | R1 | T1 | O7 |
| C03 | GROUP BY 단일 컬럼 | C2 | I1 | R2 | T1 | O7 |
| C04 | GROUP BY 2개 컬럼 | C2 | I1 | R2 | T1 | O7 |
| C05 | GROUP BY + COUNT + AVG + SUM | C2 | I1 | R2 | T1 | O7 |
| C06 | GROUP BY + HAVING | C2 | I1 | R2 | T1 | O7 |
| C07 | JOIN + GROUP BY | C3 | I1 | R3 | T3 | O7 |
| C08 | JOIN + GROUP BY + ORDER BY | C3 | I1 | R3 | T3 | O7 |
| C09 | Window Functions (ROW_NUMBER) | C3 | I1 | R3 | T1 | O8 |
| C10 | Window Functions (RANK) | C3 | I1 | R3 | T1 | O8 |
| C11 | Window Functions (PARTITION BY) | C3 | I1 | R3 | T2 | O8 |
| C12 | Multiple Window Functions | C4 | I1 | R3 | T2 | O8 |
| C13 | CTE (Common Table Expression) | C3 | I1 | R3 | T2 | O1 |
| C14 | Recursive CTE (categories 트리) | C4 | I1 | R2 | T1 | O1 |
| C15 | Multiple CTEs + JOIN | C5 | I1 | R4 | T4 | O1 |

### 카테고리 D: Bulk Operations (10개)

| ID | 설명 | 복잡도 | 반복 | 결과 | 테이블 | 작업 |
|----|------|--------|------|------|--------|------|
| D01 | BULK INSERT 100 rows | C1 | I1 | - | T1 | O5 |
| D02 | BULK INSERT 1K rows | C1 | I1 | - | T1 | O5 |
| D03 | BULK INSERT 10K rows | C1 | I1 | - | T1 | O5 |
| D04 | BULK INSERT 100K rows | C2 | I1 | - | T1 | O5 |
| D05 | BULK UPDATE 1K rows | C1 | I1 | - | T1 | O3 |
| D06 | BULK UPDATE 10K rows | C2 | I1 | - | T1 | O3 |
| D07 | BULK DELETE 1K rows | C1 | I1 | - | T1 | O4 |
| D08 | Transaction (User + Address) | C2 | I1 | - | T2 | O6 |
| D09 | Transaction (Order + Items x10) | C2 | I1 | - | T2 | O6 |
| D10 | Complex Transaction (5 tables) | C3 | I1 | - | T5 | O6 |

### 카테고리 E: Stress Tests (5개)

| ID | 설명 | 복잡도 | 반복 | 결과 | 테이블 | 작업 |
|----|------|--------|------|------|--------|------|
| E01 | 대량 조회 100K rows | C2 | I1 | R6 | T2 | O1 |
| E02 | 복잡 쿼리 1K회 반복 | C4 | I4 | R3 | T6 | O1 |
| E03 | 전체 테이블 SCAN | C2 | I1 | R6 | T1 | O1 |
| E04 | 동시성 시뮬레이션 (100 connections) | C3 | I3 | R3 | T3 | O1 |
| E05 | 메모리 압박 테스트 (Large Result) | C4 | I1 | R6 | T7 | O1 |

## 벤치마크 실행 순서

1. **Warmup Phase** (5분)
   - 각 ORM별로 Simple 시나리오 10회 실행
   - DB 캐시 워밍업
   - 커넥션 풀 초기화

2. **Measurement Phase** (60분)
   - 카테고리 A → B → C → D → E 순차 실행
   - 각 시나리오당 10회 반복 측정
   - Mean, Median, P95, P99 수집

3. **Cooldown Phase** (2분)
   - 모든 연결 종료
   - 메모리 정리

## 측정 메트릭

### 성능 메트릭
- **Latency**: Mean, Median, P95, P99 (ms)
- **Throughput**: Operations/sec
- **Memory**: Allocated, Gen0/1/2 GC
- **CPU**: User + System time

### 정확성 메트릭
- **Result Consistency**: 동일 쿼리의 결과 일치 여부
- **Data Integrity**: 쓰기 작업 후 검증

### 리소스 메트릭
- **DB Connections**: 평균/최대 연결 수
- **Network I/O**: 전송/수신 바이트
- **Disk I/O**: 읽기/쓰기 작업 수

## 예상 결과 분석

### NuVatis 예상 특성
- Simple 쿼리: Dapper와 유사 (±5%)
- Complex 쿼리: XML ResultMap 최적화로 EF Core보다 빠름 (10-30%)
- Bulk 작업: COPY 프로토콜 활용 시 Dapper와 동등
- 동적 SQL: MyBatis 스타일 조건문으로 우위

### Dapper 예상 특성
- Simple 쿼리: 가장 빠름 (baseline)
- Complex 쿼리: 수동 매핑 오버헤드 존재
- Bulk 작업: COPY 프로토콜 활용 시 최고 성능
- 메모리: 가장 적은 할당량

### EF Core 예상 특성
- Simple 쿼리: Change Tracking 오버헤드 (10-20% 느림)
- Complex 쿼리: LINQ 변환 오버헤드 (20-40% 느림)
- Bulk 작업: EF Core Extensions 없이는 매우 느림
- 메모리: 가장 많은 할당량 (DbContext, ChangeTracker)

## 대시보드 시각화 요구사항

### 화면 구성
1. **Overview Dashboard**
   - 전체 벤치마크 요약
   - ORM별 승리 카테고리 (레이더 차트)
   - 핵심 메트릭 비교 (bar chart)

2. **Category Detail**
   - 카테고리별 상세 비교 (line chart)
   - 시나리오별 드릴다운
   - 메트릭 토글 (Latency/Throughput/Memory)

3. **Scenario Comparison**
   - 3-way 비교 (NuVatis vs Dapper vs EF Core)
   - 분포 차트 (P50/P95/P99)
   - 시간 경과 그래프

4. **Resource Analysis**
   - 메모리 프로파일
   - GC 압박 분석
   - DB 연결 사용량

5. **Recommendations**
   - 시나리오별 최적 ORM 추천
   - Trade-off 분석
   - 사용 케이스별 가이드

### 기술 스택
- Frontend: React + TypeScript
- Charts: Recharts or Chart.js
- Data: BenchmarkDotNet JSON/CSV export
- Styling: Tailwind CSS
- Build: Vite

### 인터랙션
- 카테고리 필터링
- ORM 토글 (개별 숨김/표시)
- 메트릭 전환 (Latency ↔ Throughput ↔ Memory)
- 시나리오 검색
- CSV/PNG 내보내기
