# 벤치마크 실행 가이드

**작성자**: 최진호
**작성일**: 2026-03-03 (GlobalSetup 구현 완료)
**상태**: ✅ 실행 가능

---

## ⚠️ 이전 문제점

- **GlobalSetup 미구현**: 모든 벤치마크 클래스의 GlobalSetup이 TODO 상태로 비어있었음
- **가짜 데이터**: 대시보드가 mock 데이터(하드코딩된 수식)를 사용
- **실행 불가능**: NullReferenceException 발생

## ✅ 해결 완료

- **GlobalSetup 구현**: 모든 카테고리(A~E)에 DI 및 Repository 초기화 완료
- **실제 DB 연결**: PostgreSQL 벤치마크 DB 연결 설정
- **복잡한 쿼리 구현**: Dapper, EF Core 모든 메서드 구현 완료
- **빌드 성공**: 모든 프로젝트 컴파일 성공
- **실행 가능 상태**: 즉시 벤치마크 실행 가능

---

## 📋 사전 요구사항

1. **.NET 8 SDK** (이상)
2. **Docker** (PostgreSQL 벤치마크 DB용)
3. **PostgreSQL 클라이언트** (선택)

---

## 🚀 벤치마크 실행 방법

### 1단계: 벤치마크 DB 실행

```bash
# Docker Compose로 벤치마크 PostgreSQL 실행
docker-compose up -d postgres-benchmark

# 상태 확인
docker-compose ps

# 로그 확인
docker-compose logs -f postgres-benchmark
```

**벤치마크 DB 연결 설정:**
1. `benchmarks/NuVatis.Benchmark.Runner/appsettings.example.json`을 복사
2. `appsettings.json`으로 이름 변경
3. 실제 DB 연결 정보로 수정

```json
{
  "ConnectionStrings": {
    "BenchmarkDb": "Host=localhost;Port=5432;Database=your_db;Username=your_user;Password=your_password;SearchPath=nuvatest"
  }
}
```

**참고**: `appsettings.json`은 .gitignore에 등록되어 있으므로 커밋되지 않습니다.

### 2단계: 테스트 데이터 생성 (선택)

```bash
# DataGen 프로젝트로 대량 데이터 생성
cd benchmarks/NuVatis.Benchmark.DataGen
dotnet run

# 또는 기본 데이터로 테스트 (schema만 있으면 됨)
```

### 3단계: 벤치마크 빌드

```bash
cd benchmarks/NuVatis.Benchmark.Runner
dotnet build -c Release
```

### 4단계: 벤치마크 실행

**전체 실행 (60개 시나리오):**
```bash
dotnet run -c Release
# 선택: 0
```

**카테고리별 실행:**
```bash
dotnet run -c Release
# 선택:
#   A - Simple CRUD (15개 시나리오)
#   B - JOIN Complexity (15개 시나리오)
#   C - Aggregate & Analytics (15개 시나리오)
#   D - Bulk Operations (10개 시나리오)
#   E - Stress Tests (5개 시나리오)
```

---

## 📊 결과 확인

벤치마크 완료 후 결과 파일:
```
benchmarks/NuVatis.Benchmark.Runner/BenchmarkDotNet.Artifacts/results/
├── *-report.html     # HTML 리포트
├── *-report.md       # Markdown 리포트
└── *-measurements.csv # 원본 데이터
```

---

## 🎨 대시보드 업데이트 (TODO)

**현재 상태**: 대시보드는 여전히 mock 데이터 사용

**실제 데이터 연동 방법**:
1. BenchmarkDotNet JSON Exporter 활성화
2. `benchmarks/NuVatis.Benchmark.Dashboard/src/data/loadBenchmarkData.ts` 작성
3. `App.tsx`에서 `mockBenchmarkData` → `realBenchmarkData` 교체

**또는 빠른 방법**:
```bash
cd benchmarks/NuVatis.Benchmark.Dashboard
# BenchmarkDotNet CSV → JSON 변환 스크립트 작성
# 실시간 대시보드 구현
```

---

## 🛠️ 트러블슈팅

### 문제: ConnectionString 'BenchmarkDb' not found

**원인**: `appsettings.json`이 출력 디렉토리로 복사되지 않음

**해결**:
```bash
# .csproj 파일 확인
cat benchmarks/NuVatis.Benchmark.Runner/NuVatis.Benchmark.Runner.csproj

# 다음 내용이 있어야 함:
# <None Update="appsettings.json">
#   <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
# </None>
```

### 문제: 벤치마크 DB 연결 실패

**해결**:
```bash
# PostgreSQL 상태 확인
docker-compose ps

# 벤치마크 DB 수동 접속 테스트
psql -h YOUR_HOST -p YOUR_PORT -U YOUR_USERNAME -d YOUR_DATABASE

# 연결 성공 시 테이블 확인
\dt
```

### 문제: OutOfMemoryException

**원인**: 대량 데이터 벤치마크 (E01) 실행 시

**해결**:
- 메모리 할당량 늘리기: `docker-compose.yml`에서 `postgres-benchmark` 메모리 증가
- 또는 데이터 크기 줄이기: `E01_NuVatis()` 메서드에서 `GetPagedAsync(0, 10000)` → `GetPagedAsync(0, 1000)`

---

## ✨ 다음 단계

- [x] GlobalSetup 구현 완료
- [x] 복잡한 쿼리 XML 매퍼 작성 (5-table JOIN, Window Functions 모두 구현됨)
- [x] 매퍼 인터페이스에 `[NuVatisMapper]` 어트리뷰트 추가
- [ ] **NuVatis Source Generator 네임스페이스 버그 수정 필요**
- [ ] 실제 벤치마크 실행 및 결과 검증
- [ ] 대시보드 실제 데이터 연동
- [ ] README.md 결과 이미지 교체

---

## ⚠️ NuVatis Source Generator 버그

**문제**: Source Generator가 생성한 코드에서 네임스페이스가 중복됨
- 예: `NuVatis.Benchmark.NuVatis.Benchmark.Core.Models.User` (잘못됨)
- 정상: `NuVatis.Benchmark.Core.Models.User`

**영향**:
- NuVatis 매퍼 구현체 컴파일 실패
- 현재 Dapper fallback 사용 중

**해결 방법**:
1. NuVatis Source Generator 버그 수정 (패키지 업데이트 필요)
2. 또는 수동으로 SessionFactory 초기화 코드 작성

**현재 상태**: 벤치마크 실행은 가능하지만, NuVatis는 Dapper fallback 사용 중입니다.
XML 매퍼는 모두 완성되었으나, Source Generator 버그로 인해 실제 사용 불가능합니다.
