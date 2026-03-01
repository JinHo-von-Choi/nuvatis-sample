# NuVatis 대규모 ORM 벤치마크 실행 가이드

작성자: 최진호
작성일: 2026-03-01

---

## 📋 사전 준비

### 시스템 요구사항
- ✅ .NET 8.0 SDK 설치 확인: `dotnet --version`
- ✅ Docker Desktop 실행 중
- ✅ 메모리: 최소 8GB, 권장 16GB
- ✅ 디스크 여유 공간: 100GB

### 필수 도구
```bash
# .NET SDK 설치 확인
dotnet --version  # 8.0.x 이상

# Docker 실행 확인
docker --version
docker-compose --version
```

---

## 🚀 단계별 실행 가이드

### 1단계: 데이터베이스 시작

```bash
# 프로젝트 루트로 이동
cd D:/jobs/nu/nuvatis-sample

# 벤치마크 PostgreSQL 시작 (포트 5433)
docker-compose up -d postgres-benchmark

# 상태 확인
docker-compose ps

# 로그 확인 (문제 발생 시)
docker-compose logs postgres-benchmark

# 연결 테스트
docker exec -it nuvatis-benchmark-db psql -U nuvatis -d benchmark -c "SELECT version();"
```

**예상 결과:**
```
NAME                    COMMAND                  SERVICE               STATUS
nuvatis-benchmark-db    "docker-entrypoint.s…"   postgres-benchmark    Up 10 seconds (healthy)
```

---

### 2단계: 데이터베이스 스키마 적용

```bash
# 스키마 파일 확인
ls -l database/benchmark-schema.sql

# 스키마 적용 (자동 적용되지 않은 경우)
docker exec -i nuvatis-benchmark-db psql -U nuvatis -d benchmark < database/benchmark-schema.sql

# 테이블 생성 확인
docker exec -it nuvatis-benchmark-db psql -U nuvatis -d benchmark -c "\dt"
```

**예상 결과:**
```
List of relations
 Schema |      Name       | Type  |  Owner
--------+-----------------+-------+---------
 public | addresses       | table | nuvatis
 public | audit_logs      | table | nuvatis
 public | categories      | table | nuvatis
 public | coupons         | table | nuvatis
 ...
(15 rows)
```

---

### 3단계: 데이터 생성 (5-15분 소요)

```bash
# DataGen 프로젝트로 이동
cd benchmarks/NuVatis.Benchmark.DataGen

# 의존성 복원
dotnet restore

# 데이터 생성 실행 (Release 모드)
dotnet run -c Release

# 진행률 예시:
# ✓ 사용자 100,000명 생성 완료
# ✓ 카테고리 500개 생성 완료
# ✓ 상품 50,000개 생성 완료
# ✓ 주문 10,000,000건 생성 중... (2,500,000/10,000,000) 25.0%
```

**시간 예상:**
- Users (100K): ~10초
- Categories (500): ~1초
- Products (50K): ~5초
- Orders (10M): **5-10분** (가장 오래 걸림)

**중단 후 재개:**
- 중단된 경우 다시 실행하면 기존 데이터 위에 추가됨
- 완전 초기화: `docker exec -it nuvatis-benchmark-db psql -U nuvatis -d benchmark -c "TRUNCATE users CASCADE;"`

---

### 4단계: 데이터 검증

```bash
# PostgreSQL 접속
docker exec -it nuvatis-benchmark-db psql -U nuvatis -d benchmark

# 레코드 수 확인
SELECT 'users' AS table_name, COUNT(*) AS count FROM users
UNION ALL SELECT 'categories', COUNT(*) FROM categories
UNION ALL SELECT 'products', COUNT(*) FROM products
UNION ALL SELECT 'orders', COUNT(*) FROM orders
ORDER BY table_name;
```

**예상 결과:**
```
 table_name  |  count
-------------+----------
 categories  |      500
 orders      | 10000000
 products    |    50000
 users       |   100000
```

**데이터 크기 확인:**
```sql
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

**예상 결과:**
```
 schemaname | tablename  | size
------------+------------+-------
 public     | orders     | 1.2GB
 public     | users      | 45MB
 public     | products   | 12MB
```

---

### 5단계: 벤치마크 실행 (10-30분 소요)

```bash
# Runner 프로젝트로 이동
cd ../NuVatis.Benchmark.Runner

# 의존성 복원
dotnet restore

# Release 모드 실행 (중요!)
dotnet run -c Release

# 벤치마크 선택:
# 1. Simple CRUD (5개 시나리오)
# 2. Complex Query (5개 시나리오)
# 0. 모두 실행
```

**실행 흐름:**
1. Warmup (3회) - 캐시 예열, JIT 컴파일
2. Measurement (10회) - 실제 측정
3. 통계 계산 (Mean, P50, P95, P99)
4. 결과 파일 생성

**진행 중 확인:**
```
// Warmup 1: 1 op, 123.45 ms
// Warmup 2: 1 op, 98.76 ms
// Warmup 3: 1 op, 87.65 ms
// Actual 1: 1 op, 85.32 ms
// Actual 2: 1 op, 84.21 ms
...
```

---

### 6단계: 결과 확인

```bash
# 결과 디렉토리 이동
cd BenchmarkDotNet.Artifacts/results

# Markdown 리포트 확인
cat *-report.md

# CSV 데이터 확인
cat *-report.csv

# HTML 리포트 열기 (브라우저)
start *-report.html  # Windows
open *-report.html   # macOS
```

**Markdown 리포트 예시:**
```markdown
|              Method |      Mean |     Error |    StdDev |   Gen0 |  Allocated |
|-------------------- |----------:|----------:|----------:|-------:|-----------:|
|   NuVatis_GetById   |  2.345 ms | 0.0123 ms | 0.0098 ms | 15.625 |     128 KB |
|    Dapper_GetById   |  1.987 ms | 0.0101 ms | 0.0087 ms | 12.500 |     102 KB |
|    EfCore_GetById   |  3.123 ms | 0.0234 ms | 0.0198 ms | 18.750 |     156 KB |
```

**해석:**
- **Mean**: 평균 실행 시간 (낮을수록 좋음)
- **Error**: 표준 오차
- **StdDev**: 표준 편차 (낮을수록 일관성 높음)
- **Gen0**: GC Generation 0 회수 횟수
- **Allocated**: 할당된 메모리

---

## ✅ 검증 체크리스트

### 데이터 생성 검증
- [ ] Users: 100,000건
- [ ] Categories: 500건
- [ ] Products: 50,000건
- [ ] Orders: 10,000,000건

### 벤치마크 실행 검증
- [ ] Warmup 정상 완료 (3회)
- [ ] Measurement 정상 완료 (10회)
- [ ] 결과 파일 생성 (Markdown, CSV, HTML)

### 성능 기준 검증
- [ ] Simple 시나리오: 평균 <5ms
- [ ] Medium 시나리오: 평균 <50ms
- [ ] Complex 시나리오: 평균 <100ms
- [ ] 메모리 누수 없음 (GC Gen2 회수 <5회)

### 정확성 검증
- [ ] 모든 ORM이 동일한 결과 반환
- [ ] N+1 문제 최적화 효과 확인
- [ ] Keyset Paging이 OFFSET보다 빠름

---

## 🛠️ 문제 해결

### 문제: 데이터 생성 너무 느림
**원인**: PostgreSQL 설정 부족, 디스크 I/O 병목
**해결**:
```bash
# Docker 메모리 증가
# docker-compose.yml에서 limits.memory: 4GB → 8GB 수정 후 재시작
docker-compose down
docker-compose up -d postgres-benchmark

# shared_buffers 확인
docker exec -it nuvatis-benchmark-db psql -U nuvatis -d benchmark -c "SHOW shared_buffers;"
# 예상: 1GB
```

### 문제: 벤치마크 결과 불안정 (StdDev 높음)
**원인**: 백그라운드 프로세스, 캐시 불안정
**해결**:
```bash
# 1. 백그라운드 프로세스 최소화
# 2. 재실행 (Warmup 증가)
# 3. 데이터베이스 재시작
docker-compose restart postgres-benchmark
```

### 문제: 메모리 부족 (OOM)
**원인**: Docker 메모리 제한
**해결**:
```bash
# Docker Desktop → Settings → Resources → Memory: 8GB 이상
# 시스템 메모리 16GB 권장
```

### 문제: Connection timeout
**원인**: 연결 풀 고갈, 네트워크 문제
**해결**:
```json
// appsettings.json
"ConnectionStrings": {
  "Benchmark": "...;MaxPoolSize=100;Timeout=30;CommandTimeout=60"
}
```

---

## 📊 결과 분석 가이드

### NuVatis 강점 확인
- [ ] 복잡한 동적 SQL (DynamicSearch)
- [ ] 세밀한 ResultMap (FivePlusJoin)
- [ ] 계층형 쿼리 (RecursiveCTE)

### Dapper 강점 확인
- [ ] 단순 CRUD 최고 속도
- [ ] 낮은 메모리 사용
- [ ] GC 부담 최소

### EF Core 강점 확인
- [ ] LINQ 타입 안전성
- [ ] Eager Loading 편의성
- [ ] 복잡한 객체 그래프

### 약점 확인
- [ ] NuVatis XML 파싱 오버헤드
- [ ] Dapper 동적 SQL 수동 구성
- [ ] EF Core 메모리 사용량

---

## 🎯 다음 단계

1. **결과 분석**: 각 ORM의 강점/약점 파악
2. **최적화**: 느린 쿼리 인덱스 추가, 쿼리 튜닝
3. **스케일 테스트**: 데이터 2배 증가 (20M orders) 후 재측정
4. **리포트 작성**: 발견한 인사이트 문서화

---

**참고:**
- BenchmarkDotNet 공식 문서: https://benchmarkdotnet.org/
- PostgreSQL 성능 튜닝 가이드: https://wiki.postgresql.org/wiki/Performance_Optimization
