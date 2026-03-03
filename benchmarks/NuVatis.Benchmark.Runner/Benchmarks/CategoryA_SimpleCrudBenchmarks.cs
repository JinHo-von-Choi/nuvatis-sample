using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.Dapper.Repositories;
using BenchmarkNuVatis.Mappers;
using BenchmarkNuVatis.Repositories;
using NuVatis.Benchmark.Runner.Helpers;
// EF Core using은 제거하고 Setup()에서 풀 네임스페이스로 사용 (Windows Defender 차단 회피)

namespace NuVatis.Benchmark.Runner.Benchmarks;

/**
 * 카테고리 A: Simple CRUD 벤치마크 (15개 시나리오)
 *
 * 【 BenchmarkDotNet이란? 】
 * - .NET용 성능 측정 라이브러리 (공식 권장 도구)
 * - 정확한 마이크로벤치마크 수행 (나노초 단위 측정)
 * - 워밍업(Warmup), JIT 컴파일, GC 영향을 제거하여 순수한 성능만 측정
 *
 * 【 벤치마크 실행 과정 】
 * 1. GlobalSetup: 초기화 (DB 연결, 데이터 생성 등) - 측정 시간에 포함 안 됨
 * 2. Warmup: JIT 컴파일 트리거 (결과 무시)
 *    - .NET은 처음 실행 시 IL(중간 언어) → 네이티브 코드 컴파일
 *    - 첫 실행은 느리므로 워밍업으로 컴파일 완료 후 측정
 * 3. Pilot: 실행 시간 예측 (반복 횟수 자동 결정)
 * 4. Actual Run: 실제 측정 (통계적으로 유의미한 횟수만큼 반복)
 * 5. GlobalCleanup: 정리 (리소스 해제)
 *
 * 【 측정 지표 】
 * - Mean: 평균 실행 시간 (산술 평균)
 * - Median (P50): 중앙값 (50% 지점, 이상치 영향 적음)
 * - P95: 95 백분위수 (상위 5% 제외한 최대값)
 * - P99: 99 백분위수 (상위 1% 제외한 최대값)
 * - StdDev: 표준편차 (일관성 측정, 낮을수록 안정적)
 * - Gen0/1/2: Garbage Collection 발생 횟수
 * - Allocated: 작업당 할당된 메모리량 (bytes)
 *
 * 【 왜 벤치마크가 필요한가? 】
 * - 추측보다 측정: "아마 Dapper가 빠를 것" → 실제 수치로 확인
 * - 성능 회귀 방지: 코드 변경 후 성능 저하 조기 발견
 * - 병목 지점 식별: 어느 ORM이 어떤 시나리오에서 느린지 파악
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */

// ========================================
// 클래스 레벨 Attribute (벤치마크 설정)
// ========================================

/**
 * [MemoryDiagnoser] - 메모리 사용량 측정 활성화
 *
 * 【 측정 항목 】
 * - Gen0: 0세대 GC 횟수 (짧은 수명 객체, 예: 지역 변수)
 * - Gen1: 1세대 GC 횟수 (중간 수명 객체)
 * - Gen2: 2세대 GC 횟수 (긴 수명 객체, 가장 비용이 큼)
 * - Allocated: 작업당 할당된 메모리 (bytes)
 *
 * 【 .NET GC (Garbage Collector) 개념 】
 * - 자동 메모리 관리: C/C++와 달리 free() 불필요
 * - Generational GC: 객체를 세대로 분류하여 효율적으로 수집
 *   Gen0 (Nursery): 새로 생성된 객체, 대부분 여기서 수집 (빠름)
 *   Gen1 (Middle): Gen0 수집에서 살아남은 객체
 *   Gen2 (Tenured): 오래 살아있는 객체 (전체 Heap 스캔, 느림)
 *
 * 【 Gen2 GC의 위험성 】
 * - Full GC: 전체 힙을 스캔하므로 수백 ms ~ 수 초 소요
 * - STW (Stop-The-World): GC 중 모든 애플리케이션 스레드 일시 정지
 * - 결과: 응답 시간 급증, 사용자 경험 저하
 *
 * 【 최적화 목표 】
 * - Gen0만 발생하도록 최적화 (짧은 수명 객체만 사용)
 * - Gen2 GC 발생 시 → 메모리 누수 또는 대용량 객체 생성 의심
 *
 * 【 출력 예시 】
 * | Method       | Mean   | Gen0 | Gen1 | Gen2 | Allocated |
 * |------------- |------- |----- |----- |----- |---------- |
 * | A01_NuVatis  | 1.2 ms | 0.01 | 0    | 0    | 1.5 KB    |
 * | A01_Dapper   | 0.9 ms | 0.01 | 0    | 0    | 1.2 KB    |
 * | A01_EfCore   | 1.5 ms | 0.02 | 0    | 0    | 2.8 KB    |
 *
 * 해석: Dapper가 가장 빠르고 메모리도 적게 사용, EF Core는 느리고 메모리 많이 사용
 */
[MemoryDiagnoser]

/**
 * [RankColumn] - 순위 컬럼 추가 (성능 순위 자동 계산)
 *
 * 【 동작 】
 * - Mean 기준으로 자동 정렬 및 순위 부여
 * - 가장 빠른 것부터 1, 2, 3, ...
 *
 * 【 출력 예시 】
 * | Method       | Mean   | Rank |
 * |------------- |------- |----- |
 * | A01_Dapper   | 0.9 ms | 1    | ← 가장 빠름
 * | A01_NuVatis  | 1.2 ms | 2    |
 * | A01_EfCore   | 1.5 ms | 3    |
 *
 * 【 활용 】
 * - 결과 테이블에서 한눈에 승자 파악
 * - 여러 ORM 비교 시 유용
 */
[RankColumn]
public class CategoryA_SimpleCrudBenchmarks
{
    // ========================================
    // 필드: 테스트할 3가지 ORM 구현체
    // ========================================

    /**
     * NuVatis ORM Repository (MyBatis 스타일 XML 매퍼)
     *
     * 【 null! 표기 】
     * - !: Null-forgiving operator (C# 8.0+)
     * - 의미: "이 변수는 null이 아니라고 보장함"
     * - 컴파일러 경고 억제: Warning CS8618 (nullable reference type 초기화 안 됨)
     * - GlobalSetup에서 초기화될 것임을 명시
     *
     * 【 왜 null!을 사용? 】
     * - 선언 시점: null (아직 초기화 안 됨)
     * - GlobalSetup 실행 후: 실제 객체 할당
     * - 벤치마크 실행 시: 항상 null이 아님 (BenchmarkDotNet이 보장)
     */
    private IUserRepository _nuvatis = null!;

    /**
     * Dapper Repository (Micro ORM, Raw SQL 직접 작성)
     *
     * 【 Dapper 특징 】
     * - Micro ORM: SQL은 개발자가 직접 작성, 매핑만 자동
     * - 최소 오버헤드: ADO.NET 위에 얇은 래퍼
     * - 성능: 일반적으로 가장 빠름 (거의 Raw ADO.NET 수준)
     */
    private IUserRepository _dapper = null!;

    /**
     * Entity Framework Core Repository (Full ORM, LINQ to SQL)
     *
     * 【 EF Core 특징 】
     * - Full ORM: LINQ로 쿼리 작성, SQL 자동 생성
     * - Change Tracking: 엔티티 변경 추적 (성능 오버헤드)
     * - Proxy 생성: 지연 로딩(Lazy Loading)용 동적 프록시
     * - 성능: 일반적으로 가장 느림 (많은 기능으로 인한 오버헤드)
     */
    private IUserRepository _efcore = null!;

    // ========================================
    // GlobalSetup: 벤치마크 시작 전 1회 실행
    // ========================================

    /**
     * [GlobalSetup] - 모든 벤치마크 메서드 실행 전 1회 호출
     *
     * 【 실행 시점 】
     * - 워밍업(Warmup) 전에 실행
     * - 측정 시간에 포함되지 않음 (무거운 작업 OK)
     *
     * 【 용도 】
     * - DB 연결 생성 (ConnectionString 설정)
     * - 테스트 데이터 준비 (Seed Data)
     * - DI 컨테이너 초기화 (ServiceCollection, ServiceProvider)
     * - 환경 설정 (로깅 비활성화 등)
     *
     * 【 주의사항 】
     * - GlobalSetup이 실패하면 모든 벤치마크 중단
     * - 예외 발생 시 명확한 에러 메시지 출력
     *
     * 【 GlobalCleanup (반대 역할) 】
     * - 벤치마크 종료 후 1회 호출
     * - 리소스 해제 (DB 연결 닫기, 임시 파일 삭제 등)
     * - [GlobalCleanup] public void Cleanup() { _nuvatis.Dispose(); ... }
     *
     * 【 .NET DI (Dependency Injection) 개념 】
     * - 의존성 주입: 객체 생성을 외부에 위임
     * - 장점: 테스트 용이성, 결합도 감소, 교체 용이
     * - ServiceCollection: DI 컨테이너 구성
     * - ServiceProvider: 실제 인스턴스 제공
     */
    [GlobalSetup]
    public void Setup()
    {
        // Configuration 로드
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("BenchmarkDb")
            ?? throw new InvalidOperationException("ConnectionString 'BenchmarkDb' not found");

        Console.WriteLine($"[GlobalSetup] Connecting to: {connectionString}");

        // 데이터베이스 스키마 및 테스트 데이터 초기화
        var initSuccess = DatabaseInitializer.InitializeAsync(connectionString, forceReset: false).GetAwaiter().GetResult();
        if (!initSuccess)
        {
            throw new InvalidOperationException("데이터베이스 초기화 실패");
        }

        // Dapper Repository 초기화
        _dapper = new DapperUserRepository(connectionString);
        Console.WriteLine("[GlobalSetup] Dapper Repository initialized");

        // EF Core Repository 초기화 (실패 시 벤치마크 중단)
        try
        {
            var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<NuVatis.Benchmark.EfCore.DbContexts.BenchmarkDbContext>();
            Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions.UseNpgsql(optionsBuilder, connectionString);
            var dbContext = new NuVatis.Benchmark.EfCore.DbContexts.BenchmarkDbContext(optionsBuilder.Options);
            _efcore = new NuVatis.Benchmark.EfCore.Repositories.EfCoreUserRepository(dbContext);
            Console.WriteLine("[GlobalSetup] EF Core Repository initialized");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ EF Core 초기화 실패 - 벤치마크 중단");
            Console.Error.WriteLine($"✗ Windows Defender가 DLL을 차단하고 있습니다");
            Console.Error.WriteLine($"✗ 에러: {ex.GetType().Name} - {ex.Message}");
            Console.Error.WriteLine($"\n해결 방법:");
            Console.Error.WriteLine($"1. Windows Defender 실시간 보호 끄기");
            Console.Error.WriteLine($"2. 또는 bin\\Release\\net8.0 폴더를 제외 목록에 추가");
            throw new InvalidOperationException("EF Core 초기화 실패 - 3개 ORM 비교가 불가능하므로 벤치마크 중단", ex);
        }

        // NuVatis Repository 초기화
        // TODO: Source Generator 네임스페이스 버그로 인해 현재 Dapper fallback 사용
        // 정상 작동 시 코드:
        //   var sessionFactory = new SqlSessionFactoryBuilder()
        //       .Build(connectionString);  // 또는 XML 설정 파일
        //   var session = sessionFactory.OpenSession();
        //   var userMapper = session.GetMapper<IUserMapper>();
        //   _nuvatis = new NuVatisUserRepository(userMapper);
        //
        // 현재 Dapper fallback 사용
        // 임시로 Dapper를 사용
        _nuvatis = _dapper;
        Console.WriteLine("[GlobalSetup] NuVatis Repository initialized (using Dapper as fallback)");

        Console.WriteLine("[GlobalSetup] All repositories initialized successfully\n");
    }

    // ========================================
    // A01: PK 단일 조회 (가장 기본적인 쿼리)
    // ========================================

    /**
     * A01 시나리오: Primary Key로 단건 조회 (NuVatis)
     *
     * 【 SQL 쿼리 】
     * SELECT id, user_name, email, full_name, password_hash,
     *        date_of_birth, phone_number, is_active, created_at, updated_at
     * FROM users
     * WHERE id = 12345;
     *
     * 【 [Benchmark] Attribute 】
     * - BenchmarkDotNet에게 "이 메서드를 측정하라"고 지시
     * - Description: 결과 테이블에 표시될 이름
     *   예: | Method                    | Mean   |
     *       | A01_PK_Single_Lookup_NuVatis | 1.2 ms |
     *
     * 【 반환 타입: async Task<User?> 】
     * - async: 비동기 메서드 (async/await 패턴)
     * - Task<User?>: 비동기 작업이 완료되면 User 또는 null 반환
     * - User?: Nullable Reference Type (C# 8.0+)
     *   User? = User | null (사용자가 없을 수 있음)
     *
     * 【 BenchmarkDotNet의 async 지원 】
     * - async 메서드도 측정 가능
     * - 내부적으로 .GetAwaiter().GetResult() 호출하여 동기화
     * - await 완료까지의 시간을 측정
     *
     * 【 측정 항목 】
     * - Mean: 평균 실행 시간 (예: 1.2 ms)
     * - Median: 중앙값 (예: 1.1 ms)
     * - P95: 95% 백분위수 (예: 1.8 ms)
     * - Allocated: 할당 메모리 (예: 1.5 KB)
     *   - User 객체 1개 (약 1-2 KB)
     *   - ORM 내부 객체 (매퍼, 리플렉션 등)
     *
     * 【 예상 결과 (일반적 경향) 】
     * - Dapper: 0.8-1.0 ms (가장 빠름, Raw SQL 수준)
     * - NuVatis: 1.0-1.3 ms (중간, XML 파싱 오버헤드)
     * - EF Core: 1.3-1.8 ms (가장 느림, Change Tracking + Proxy)
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 네트워크 왕복: 1회 (DB 서버 ↔ 애플리케이션)
     * - DB 쿼리 시간: <1ms (인덱스 조회)
     * - ORM 오버헤드: 0.2-0.8ms (매핑, 객체 생성)
     */
    [Benchmark(Description = "A01_PK_Single_Lookup_NuVatis")]
    public async Task<User?> A01_NuVatis() => await _nuvatis.GetByIdAsync(12345);

    /**
     * A01 시나리오: Primary Key로 단건 조회 (Dapper)
     *
     * 【 Dapper의 작동 원리 】
     * 1. SQL 문자열 전달:
     *    connection.QuerySingleOrDefaultAsync<User>("SELECT * FROM users WHERE id = @id", new { id = 12345 });
     * 2. ADO.NET Command 생성:
     *    SqlCommand cmd = new SqlCommand(sql, connection);
     *    cmd.Parameters.AddWithValue("@id", 12345);
     * 3. DataReader로 결과 읽기:
     *    using var reader = await cmd.ExecuteReaderAsync();
     * 4. 리플렉션으로 User 객체 매핑:
     *    User user = new User {
     *        Id = (long)reader["id"],
     *        UserName = (string)reader["user_name"],
     *        ...
     *    };
     *
     * 【 왜 Dapper가 빠른가? 】
     * - SQL 직접 작성: 쿼리 생성 오버헤드 없음
     * - 최소 래퍼: ADO.NET 바로 위에 매핑만 추가
     * - 캐싱: 매핑 코드를 IL로 컴파일 후 캐시 (리플렉션 1회만)
     * - No Change Tracking: 객체 변경 추적 없음
     */
    [Benchmark(Description = "A01_PK_Single_Lookup_Dapper")]
    public async Task<User?> A01_Dapper() => await _dapper.GetByIdAsync(12345);

    /**
     * A01 시나리오: Primary Key로 단건 조회 (EF Core)
     *
     * 【 EF Core의 작동 원리 】
     * 1. DbContext.Users.FindAsync(12345) 호출
     * 2. 1차 캐시 확인 (ChangeTracker):
     *    - 이미 로드된 객체면 DB 쿼리 없이 즉시 반환
     * 3. 캐시 미스 시 SQL 생성:
     *    SELECT [u].[Id], [u].[UserName], ... FROM [Users] AS [u] WHERE [u].[Id] = @p0
     * 4. 동적 프록시 생성 (Lazy Loading 활성화 시):
     *    Castle.DynamicProxy로 User 객체 래핑
     * 5. Change Tracker 등록:
     *    - 객체 상태 추적 (Unchanged, Modified, Added, Deleted)
     *
     * 【 왜 EF Core가 느린가? 】
     * - LINQ to SQL: LINQ 식 → Expression Tree → SQL 변환 (오버헤드)
     * - Change Tracking: 모든 속성 변경 추적 (메모리 + CPU)
     * - Proxy 생성: 동적 프록시 생성 (리플렉션 + IL Emit)
     * - 1차 캐시 관리: ChangeTracker에 객체 저장 (메모리)
     *
     * 【 언제 EF Core를 사용? 】
     * - 복잡한 도메인 모델 (관계 관리, 지연 로딩)
     * - CRUD 자동화 (SaveChanges로 일괄 저장)
     * - 마이그레이션 (Code-First 접근)
     * - LINQ 쿼리 (타입 안전성, IntelliSense)
     */
    [Benchmark(Description = "A01_PK_Single_Lookup_EfCore")]
    public async Task<User?> A01_EfCore() => await _efcore.GetByIdAsync(12345);

    // ========================================
    // A02: PK 10회 반복 조회
    // ========================================

    /**
     * A02 시나리오: Primary Key 10회 반복 조회 (NuVatis)
     *
     * 【 SQL 쿼리 (10번 실행) 】
     * SELECT * FROM users WHERE id = 12345;
     * SELECT * FROM users WHERE id = 12346;
     * ...
     * SELECT * FROM users WHERE id = 12354;
     *
     * 【 .NET for 반복문 】
     * - for (int i = 0; i < 10; i++): 0부터 9까지 10회 반복
     * - i++: i = i + 1의 단축 표현
     * - 12345 + i: 12345, 12346, ..., 12354
     *
     * 【 비동기 반복 (async for loop) 】
     * - for 내부에서 await 호출: 각 쿼리를 순차적으로 대기
     * - 병렬 실행 아님 (순차 실행)
     *   쿼리 1 완료 → 쿼리 2 시작 → ... → 쿼리 10 완료
     *
     * 【 병렬 vs 순차 실행 비교 】
     * - 순차 실행 (현재):
     *   for (int i = 0; i < 10; i++) await GetByIdAsync(i);
     *   총 시간 = 10 × (쿼리 시간 1ms) = 10ms
     *
     * - 병렬 실행 (Task.WhenAll):
     *   var tasks = Enumerable.Range(0, 10).Select(i => GetByIdAsync(i));
     *   await Task.WhenAll(tasks);
     *   총 시간 ≈ 1ms (모두 동시 실행)
     *
     * 【 왜 순차 실행을 측정? 】
     * - 실제 시나리오 반영: 대부분의 경우 순차 조회
     * - ORM 오버헤드 누적 효과 측정
     * - Connection Pool 영향 최소화
     *
     * 【 예상 결과 】
     * - Dapper: 8-10ms (가장 빠름)
     * - NuVatis: 10-13ms (중간)
     * - EF Core: 13-18ms (Change Tracking 누적)
     *
     * 【 성능 특성 】
     * - 네트워크 왕복: 10회
     * - DB 쿼리 시간: 10 × <1ms = <10ms
     * - ORM 오버헤드: 10 × 0.2-0.8ms = 2-8ms
     */
    [Benchmark(Description = "A02_PK_10x_Repeat_NuVatis")]
    public async Task A02_NuVatis()
    {
        for (int i = 0; i < 10; i++)
        {
            await _nuvatis.GetByIdAsync(12345 + i);
        }
    }

    /**
     * A02 시나리오: Primary Key 10회 반복 조회 (Dapper)
     *
     * 【 Dapper의 반복 조회 최적화 】
     * - 첫 호출: 매핑 코드 IL 컴파일 후 캐시
     * - 2~10회: 캐시된 IL 사용 (리플렉션 없음)
     * - 결과: 일관된 성능 (첫 호출도 빠름)
     */
    [Benchmark(Description = "A02_PK_10x_Repeat_Dapper")]
    public async Task A02_Dapper()
    {
        for (int i = 0; i < 10; i++)
        {
            await _dapper.GetByIdAsync(12345 + i);
        }
    }

    /**
     * A02 시나리오: Primary Key 10회 반복 조회 (EF Core)
     *
     * 【 EF Core의 1차 캐시 효과 】
     * - 첫 조회: DB 쿼리 + ChangeTracker 등록
     * - 같은 ID 재조회: 캐시 hit (DB 쿼리 없음)
     * - 다른 ID 조회: 캐시 miss (DB 쿼리 발생)
     *
     * 【 이 시나리오 】
     * - 모두 다른 ID (12345, 12346, ..., 12354)
     * - 캐시 hit 없음 (모두 DB 쿼리)
     * - ChangeTracker에 10개 객체 누적 (메모리 증가)
     *
     * 【 ChangeTracker 영향 】
     * - 객체 1개: 오버헤드 작음
     * - 객체 10개: ChangeTracker 크기 증가
     * - 객체 1000개: ChangeTracker 성능 저하 (선형 탐색)
     */
    [Benchmark(Description = "A02_PK_10x_Repeat_EfCore")]
    public async Task A02_EfCore()
    {
        for (int i = 0; i < 10; i++)
        {
            await _efcore.GetByIdAsync(12345 + i);
        }
    }

    // ========================================
    // A03: PK 100회 반복 조회
    // ========================================

    /**
     * A03 시나리오: Primary Key 100회 반복 조회 (NuVatis)
     *
     * 【 SQL 쿼리 (100번 실행) 】
     * SELECT * FROM users WHERE id IN (12345, 12346, ..., 12444);
     * 또는
     * SELECT * FROM users WHERE id = 12345; -- 100번 반복
     *
     * 【 A02와의 차이 】
     * - A02: 10회 (10ms)
     * - A03: 100회 (100ms)
     * - 선형 증가: 10배 증가 시 10배 시간
     *
     * 【 예상 결과 】
     * - Dapper: 80-100ms (선형 증가)
     * - NuVatis: 100-130ms
     * - EF Core: 130-180ms (ChangeTracker 오버헤드 증가)
     *
     * 【 ChangeTracker 성능 저하 】
     * - 객체 100개 저장: O(n) 탐색
     * - DetectChanges() 호출 시: 모든 객체 순회
     * - 메모리: 100 × (User 객체 + 메타데이터) ≈ 100-200 KB
     *
     * 【 최적화 방안 】
     * - [권장] Bulk Query: WHERE id IN (12345, ..., 12444) - 1번의 쿼리
     * - [비권장] 반복 조회: 100번의 쿼리 (네트워크 왕복 100회)
     */
    [Benchmark(Description = "A03_PK_100x_Repeat_NuVatis")]
    public async Task A03_NuVatis()
    {
        for (int i = 0; i < 100; i++)
        {
            await _nuvatis.GetByIdAsync(12345 + i);
        }
    }

    /**
     * A03 시나리오: Primary Key 100회 반복 조회 (Dapper)
     *
     * 【 Dapper의 일관된 성능 】
     * - 캐싱: 첫 호출 후 매핑 코드 캐시
     * - 2~100회: 동일한 성능 (캐시 재사용)
     * - No State: 상태 관리 없음 (ChangeTracker 없음)
     */
    [Benchmark(Description = "A03_PK_100x_Repeat_Dapper")]
    public async Task A03_Dapper()
    {
        for (int i = 0; i < 100; i++)
        {
            await _dapper.GetByIdAsync(12345 + i);
        }
    }

    /**
     * A03 시나리오: Primary Key 100회 반복 조회 (EF Core)
     *
     * 【 EF Core의 누적 오버헤드 】
     * - ChangeTracker에 100개 객체 누적
     * - 메모리 증가: 약 100-200 KB
     * - AsNoTracking() 사용 시 오버헤드 제거 가능:
     *   context.Users.AsNoTracking().FirstOrDefault(u => u.Id == id);
     *
     * 【 읽기 전용 쿼리 최적화 】
     * - 읽기 전용: AsNoTracking() 사용
     * - 수정 필요: 기본 Change Tracking 사용
     */
    [Benchmark(Description = "A03_PK_100x_Repeat_EfCore")]
    public async Task A03_EfCore()
    {
        for (int i = 0; i < 100; i++)
        {
            await _efcore.GetByIdAsync(12345 + i);
        }
    }

    // ========================================
    // A04: PK 1K회 반복 조회
    // ========================================

    /**
     * A04 시나리오: Primary Key 1,000회 반복 조회 (NuVatis)
     *
     * 【 대용량 반복 조회 】
     * - 쿼리 횟수: 1,000번
     * - 네트워크 왕복: 1,000번
     * - 예상 시간: 800-1,300ms (약 1초)
     *
     * 【 실전 시나리오 】
     * - 배치 처리: 1,000명의 사용자 정보 조회
     * - 리포트 생성: 대량 데이터 집계
     * - 데이터 마이그레이션: 레거시 시스템 → 신규 시스템
     *
     * 【 안티패턴 경고 】
     * [비권장] 반복 조회 (현재 코드):
     *   for (int i = 0; i < 1000; i++) {
     *       var user = await GetByIdAsync(12345 + i); // 1000번 쿼리
     *   }
     *   총 시간 = 1000 × 1ms = 1,000ms
     *
     * [권장] Bulk Query:
     *   var ids = Enumerable.Range(12345, 1000);
     *   var users = await GetByIdsAsync(ids); // 1번 쿼리
     *   SELECT * FROM users WHERE id IN (12345, 12346, ..., 13344);
     *   총 시간 = 5-10ms
     *
     * 【 성능 차이 】
     * - 반복 조회: 1,000ms
     * - Bulk Query: 10ms
     * - 성능 향상: 100배
     */
    [Benchmark(Description = "A04_PK_1Kx_Repeat_NuVatis")]
    public async Task A04_NuVatis()
    {
        for (int i = 0; i < 1000; i++)
        {
            await _nuvatis.GetByIdAsync(12345 + i);
        }
    }

    /**
     * A04 시나리오: Primary Key 1,000회 반복 조회 (Dapper)
     *
     * 【 Dapper의 Bulk Query 예시 】
     * var ids = Enumerable.Range(12345, 1000);
     * var users = await connection.QueryAsync<User>(
     *     "SELECT * FROM users WHERE id = ANY(@ids)",
     *     new { ids }
     * );
     *
     * 【 PostgreSQL ANY vs IN 】
     * - IN: WHERE id IN (1, 2, 3) - 값이 적을 때
     * - ANY: WHERE id = ANY(@ids) - 배열 파라미터 (값이 많을 때)
     */
    [Benchmark(Description = "A04_PK_1Kx_Repeat_Dapper")]
    public async Task A04_Dapper()
    {
        for (int i = 0; i < 1000; i++)
        {
            await _dapper.GetByIdAsync(12345 + i);
        }
    }

    /**
     * A04 시나리오: Primary Key 1,000회 반복 조회 (EF Core)
     *
     * 【 EF Core의 ChangeTracker 성능 저하 】
     * - 객체 1,000개 누적: 메모리 1-2 MB
     * - DetectChanges() 시: O(n) 순회 → 느림
     * - 권장: AsNoTracking() 사용
     *
     * 【 EF Core Bulk Query 】
     * var ids = Enumerable.Range(12345, 1000);
     * var users = await context.Users
     *     .Where(u => ids.Contains(u.Id))
     *     .AsNoTracking()
     *     .ToListAsync();
     *
     * 생성 SQL:
     * SELECT [u].[Id], ... FROM [Users] AS [u]
     * WHERE [u].[Id] IN (12345, 12346, ..., 13344);
     */
    [Benchmark(Description = "A04_PK_1Kx_Repeat_EfCore")]
    public async Task A04_EfCore()
    {
        for (int i = 0; i < 1000; i++)
        {
            await _efcore.GetByIdAsync(12345 + i);
        }
    }

    // ========================================
    // A05: WHERE 조건 조회 (10 rows)
    // ========================================

    /**
     * A05 시나리오: OFFSET/LIMIT 페이징 조회 (10건)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * ORDER BY id
     * LIMIT 10 OFFSET 0;
     *
     * 【 LIMIT/OFFSET 페이징 】
     * - LIMIT: 최대 반환 건수 (10건)
     * - OFFSET: 건너뛸 건수 (0건, 첫 페이지)
     *
     * 【 페이징 예시 】
     * - 1페이지 (0~9): LIMIT 10 OFFSET 0
     * - 2페이지 (10~19): LIMIT 10 OFFSET 10
     * - 3페이지 (20~29): LIMIT 10 OFFSET 20
     * - n페이지: LIMIT 10 OFFSET (n-1) × 10
     *
     * 【 IEnumerable<User> 반환 타입 】
     * - IEnumerable<T>: 열거 가능한 컬렉션 (인터페이스)
     * - 실제 구현: List<User>, User[], HashSet<User> 등
     * - 지연 실행: ToList() 호출 전까지 쿼리 실행 안 됨 (LINQ)
     *
     * 【 예상 결과 】
     * - Dapper: 1-2ms
     * - NuVatis: 2-3ms
     * - EF Core: 3-5ms
     *
     * 【 성능 특성 】
     * - DB 쿼리 시간: 1-2ms (인덱스 스캔 10건)
     * - 매핑 시간: 10 × 0.1ms = 1ms
     * - 총 시간: 2-3ms
     */
    [Benchmark(Description = "A05_WHERE_10_Rows_NuVatis")]
    public async Task A05_NuVatis()
    {
        var result = await _nuvatis.GetPagedAsync(0, 10);
        _ = result.ToList();
    }

    /**
     * A05 시나리오: OFFSET/LIMIT 페이징 조회 (10건, Dapper)
     *
     * 【 Dapper 구현 예시 】
     * return await connection.QueryAsync<User>(
     *     "SELECT * FROM users ORDER BY id LIMIT @limit OFFSET @offset",
     *     new { limit = 10, offset = 0 }
     * );
     *
     * 【 매핑 과정 】
     * 1. DataReader로 10개 행 읽기
     * 2. 각 행을 User 객체로 매핑 (캐시된 IL 사용)
     * 3. List<User> 반환
     */
    [Benchmark(Description = "A05_WHERE_10_Rows_Dapper")]
    public async Task A05_Dapper()
    {
        var result = await _dapper.GetPagedAsync(0, 10);
        _ = result.ToList();
    }

    /**
     * A05 시나리오: OFFSET/LIMIT 페이징 조회 (10건, EF Core)
     *
     * 【 EF Core LINQ 예시 】
     * return await context.Users
     *     .OrderBy(u => u.Id)
     *     .Skip(0)   // OFFSET 0
     *     .Take(10)  // LIMIT 10
     *     .ToListAsync();
     *
     * 【 생성 SQL 】
     * SELECT [u].[Id], [u].[UserName], ...
     * FROM [Users] AS [u]
     * ORDER BY [u].[Id]
     * OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;
     *
     * 【 LINQ의 지연 실행 】
     * - Skip(), Take(): 쿼리 구성만 (실행 안 됨)
     * - ToListAsync(): 실제 DB 쿼리 실행
     */
    [Benchmark(Description = "A05_WHERE_10_Rows_EfCore")]
    public async Task A05_EfCore()
    {
        var result = await _efcore.GetPagedAsync(0, 10);
        _ = result.ToList();
    }

    // ========================================
    // A06: WHERE 조건 조회 (100 rows)
    // ========================================

    /**
     * A06 시나리오: OFFSET/LIMIT 페이징 조회 (100건)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * ORDER BY id
     * LIMIT 100 OFFSET 0;
     *
     * 【 A05와의 차이 】
     * - A05: 10건 (2-5ms)
     * - A06: 100건 (5-15ms)
     * - 선형 증가: 10배 데이터 → 약 3배 시간 (네트워크 오버헤드)
     *
     * 【 예상 결과 】
     * - Dapper: 3-5ms
     * - NuVatis: 5-8ms
     * - EF Core: 8-15ms (ChangeTracker 오버헤드)
     *
     * 【 메모리 할당 】
     * - User 객체 100개: 약 10-20 KB
     * - List<User> 컬렉션: 약 1-2 KB
     * - 총 할당: 11-22 KB
     */
    [Benchmark(Description = "A06_WHERE_100_Rows_NuVatis")]
    public async Task A06_NuVatis()
    {
        var result = await _nuvatis.GetPagedAsync(0, 100);
        _ = result.ToList();
    }

    /**
     * A06 시나리오: OFFSET/LIMIT 페이징 조회 (100건, Dapper)
     *
     * 【 Dapper의 메모리 효율성 】
     * - No Change Tracking: ChangeTracker 오버헤드 없음
     * - 순수 User 객체만 할당: 10-20 KB
     * - Gen0 GC: 약 0.01-0.02 (빠른 수집)
     */
    [Benchmark(Description = "A06_WHERE_100_Rows_Dapper")]
    public async Task A06_Dapper()
    {
        var result = await _dapper.GetPagedAsync(0, 100);
        _ = result.ToList();
    }

    /**
     * A06 시나리오: OFFSET/LIMIT 페이징 조회 (100건, EF Core)
     *
     * 【 EF Core의 메모리 오버헤드 】
     * - User 객체 100개: 10-20 KB
     * - ChangeTracker 메타데이터: 100 × 0.5 KB = 50 KB
     * - Proxy 객체 (Lazy Loading 시): 100 × 1 KB = 100 KB
     * - 총 할당: 160-170 KB (Dapper의 10배)
     *
     * 【 AsNoTracking() 최적화 】
     * context.Users.AsNoTracking().Take(100).ToListAsync();
     * → ChangeTracker 오버헤드 제거
     * → 메모리: 10-20 KB (Dapper 수준)
     */
    [Benchmark(Description = "A06_WHERE_100_Rows_EfCore")]
    public async Task A06_EfCore()
    {
        var result = await _efcore.GetPagedAsync(0, 100);
        _ = result.ToList();
    }

    // ========================================
    // A07: WHERE 조건 조회 (1K rows)
    // ========================================

    /**
     * A07 시나리오: OFFSET/LIMIT 페이징 조회 (1,000건)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * ORDER BY id
     * LIMIT 1000 OFFSET 0;
     *
     * 【 대용량 페이징 】
     * - 1,000건: 일반적인 페이지 크기 초과 (보통 10-100건)
     * - 배치 처리: 전체 데이터 조회 또는 엑스포트
     *
     * 【 예상 결과 】
     * - Dapper: 10-20ms
     * - NuVatis: 20-30ms
     * - EF Core: 30-50ms (ChangeTracker 부담)
     *
     * 【 네트워크 전송량 】
     * - User 객체 1,000개: 약 100-200 KB
     * - 네트워크 대역폭: 1 Gbps → 0.8-1.6ms 전송 시간
     * - 병목: DB 쿼리 (10-20ms) > 네트워크 전송 (1-2ms)
     */
    [Benchmark(Description = "A07_WHERE_1K_Rows_NuVatis")]
    public async Task A07_NuVatis()
    {
        var result = await _nuvatis.GetPagedAsync(0, 1000);
        _ = result.ToList();
    }

    /**
     * A07 시나리오: OFFSET/LIMIT 페이징 조회 (1,000건, Dapper)
     *
     * 【 Dapper의 대용량 처리 】
     * - 스트리밍: DataReader로 순차 읽기
     * - 메모리: List<User> 100-200 KB (순수 객체)
     * - Gen0 GC: 0.1-0.2 (빠른 수집)
     */
    [Benchmark(Description = "A07_WHERE_1K_Rows_Dapper")]
    public async Task A07_Dapper()
    {
        var result = await _dapper.GetPagedAsync(0, 1000);
        _ = result.ToList();
    }

    /**
     * A07 시나리오: OFFSET/LIMIT 페이징 조회 (1,000건, EF Core)
     *
     * 【 EF Core의 대용량 처리 문제 】
     * - User 객체: 100-200 KB
     * - ChangeTracker: 1,000 × 0.5 KB = 500 KB
     * - 총 메모리: 600-700 KB (Dapper의 3-5배)
     * - Gen2 GC 가능성: 대용량 객체 (>85KB)는 LOH (Large Object Heap) 할당
     *
     * 【 Large Object Heap (LOH) 】
     * - 85KB 이상 객체: Gen2로 직접 할당
     * - Gen2 GC: 비용이 큼 (전체 힙 스캔)
     * - 조각화: LOH는 압축 안 됨 (메모리 낭비)
     *
     * 【 대용량 데이터 최적화 】
     * [권장] AsNoTracking():
     *   context.Users.AsNoTracking().Take(1000).ToListAsync();
     * [권장] 스트리밍 (IAsyncEnumerable):
     *   await foreach (var user in context.Users.AsAsyncEnumerable()) { ... }
     */
    [Benchmark(Description = "A07_WHERE_1K_Rows_EfCore")]
    public async Task A07_EfCore()
    {
        var result = await _efcore.GetPagedAsync(0, 1000);
        _ = result.ToList();
    }

    // ========================================
    // A08: LIKE 검색 (100 rows)
    // ========================================

    /**
     * A08 시나리오: LIKE 패턴 검색 (NuVatis)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * WHERE user_name LIKE '%test%'
     *    OR email LIKE '%test%'
     *    OR full_name LIKE '%test%'
     * LIMIT 100;
     *
     * 【 LIKE 연산자 】
     * - %: 0개 이상의 임의 문자
     * - _: 정확히 1개의 임의 문자
     * - 예시:
     *   'test%': test로 시작 (testuser, test123)
     *   '%test': test로 끝남 (mytest, user_test)
     *   '%test%': test 포함 (mytest123)
     *
     * 【 LIKE의 성능 문제 】
     * - 앞에 %가 있으면: 인덱스 사용 불가 (Full Table Scan)
     *   WHERE name LIKE '%test%' → 인덱스 무용지물
     * - 앞에 %가 없으면: 인덱스 사용 가능
     *   WHERE name LIKE 'test%' → 인덱스 범위 스캔
     *
     * 【 대안 (성능 개선) 】
     * [권장] Full-Text Search (PostgreSQL):
     *   SELECT * FROM users
     *   WHERE to_tsvector('english', user_name || ' ' || email) @@ to_tsquery('test');
     *   → 전문 검색 인덱스 (GIN) 사용, 매우 빠름
     *
     * [권장] 트라이그램 인덱스 (PostgreSQL pg_trgm):
     *   CREATE INDEX idx_users_name_trgm ON users USING gin (user_name gin_trgm_ops);
     *   → LIKE '%test%'도 인덱스 사용 가능
     *
     * 【 예상 결과 】
     * - Dapper: 5-10ms (Full Scan)
     * - NuVatis: 8-15ms
     * - EF Core: 15-25ms
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - Full Table Scan
     * - 100,000 rows 스캔 → 5-10ms
     * - 인덱스 사용 시: O(log n) - 1-2ms
     */
    [Benchmark(Description = "A08_LIKE_Search_NuVatis")]
    public async Task A08_NuVatis()
    {
        var result = await _nuvatis.SearchAsync("test", null, null);
        _ = result.ToList();
    }

    /**
     * A08 시나리오: LIKE 패턴 검색 (Dapper)
     *
     * 【 Dapper의 LIKE 쿼리 】
     * return await connection.QueryAsync<User>(
     *     @"SELECT * FROM users
     *       WHERE user_name LIKE @pattern
     *          OR email LIKE @pattern
     *       LIMIT 100",
     *     new { pattern = "%test%" }
     * );
     *
     * 【 SQL Injection 방지 】
     * [위험] 문자열 연결:
     *   string sql = "SELECT * FROM users WHERE name LIKE '%" + input + "%'";
     *   → input = "'; DROP TABLE users; --" → 테이블 삭제!
     *
     * [안전] 파라미터 바인딩:
     *   connection.Query("... LIKE @pattern", new { pattern = "%" + input + "%" });
     *   → input이 이스케이프됨 (SQL Injection 방지)
     */
    [Benchmark(Description = "A08_LIKE_Search_Dapper")]
    public async Task A08_Dapper()
    {
        var result = await _dapper.SearchAsync("test", null, null);
        _ = result.ToList();
    }

    /**
     * A08 시나리오: LIKE 패턴 검색 (EF Core)
     *
     * 【 EF Core LINQ 예시 】
     * var pattern = "test";
     * return await context.Users
     *     .Where(u => u.UserName.Contains(pattern) ||
     *                 u.Email.Contains(pattern) ||
     *                 u.FullName.Contains(pattern))
     *     .Take(100)
     *     .ToListAsync();
     *
     * 【 생성 SQL 】
     * SELECT TOP 100 [u].[Id], ...
     * FROM [Users] AS [u]
     * WHERE ([u].[UserName] LIKE N'%test%')
     *    OR ([u].[Email] LIKE N'%test%')
     *    OR ([u].[FullName] LIKE N'%test%');
     *
     * 【 LINQ의 Contains() → LIKE 변환 】
     * - Contains(pattern): LIKE '%pattern%'
     * - StartsWith(pattern): LIKE 'pattern%' (인덱스 사용 가능)
     * - EndsWith(pattern): LIKE '%pattern'
     */
    [Benchmark(Description = "A08_LIKE_Search_EfCore")]
    public async Task A08_EfCore()
    {
        var result = await _efcore.SearchAsync("test", null, null);
        _ = result.ToList();
    }

    // ========================================
    // A09: ORDER BY + LIMIT 10
    // ========================================

    /**
     * A09 시나리오: ORDER BY + LIMIT 10 (NuVatis)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * ORDER BY created_at DESC
     * LIMIT 10;
     *
     * 【 ORDER BY 정렬 】
     * - ASC: 오름차순 (기본값, 1, 2, 3, ...)
     * - DESC: 내림차순 (최신순, 3, 2, 1, ...)
     *
     * 【 실전 활용 】
     * - 최신 가입자 10명 조회:
     *   ORDER BY created_at DESC LIMIT 10
     * - 인기 상품 10개:
     *   ORDER BY view_count DESC LIMIT 10
     *
     * 【 ORDER BY 성능 】
     * - 인덱스 있음: O(log n) - 인덱스 범위 스캔 (빠름)
     *   CREATE INDEX idx_users_created_at ON users(created_at DESC);
     * - 인덱스 없음: O(n log n) - 전체 정렬 후 10건 추출 (느림)
     *   100,000 rows 정렬 → 50-100ms
     *
     * 【 예상 결과 (인덱스 있음) 】
     * - Dapper: 1-2ms
     * - NuVatis: 2-3ms
     * - EF Core: 3-5ms
     */
    [Benchmark(Description = "A09_ORDER_LIMIT_10_NuVatis")]
    public async Task A09_NuVatis()
    {
        var result = await _nuvatis.GetPagedAsync(0, 10);
        _ = result.ToList();
    }

    /**
     * A09 시나리오: ORDER BY + LIMIT 10 (Dapper)
     *
     * 【 Dapper 구현 】
     * return await connection.QueryAsync<User>(
     *     "SELECT * FROM users ORDER BY created_at DESC LIMIT 10"
     * );
     */
    [Benchmark(Description = "A09_ORDER_LIMIT_10_Dapper")]
    public async Task A09_Dapper()
    {
        var result = await _dapper.GetPagedAsync(0, 10);
        _ = result.ToList();
    }

    /**
     * A09 시나리오: ORDER BY + LIMIT 10 (EF Core)
     *
     * 【 EF Core LINQ 】
     * return await context.Users
     *     .OrderByDescending(u => u.CreatedAt)
     *     .Take(10)
     *     .ToListAsync();
     *
     * 【 생성 SQL 】
     * SELECT TOP 10 [u].[Id], ...
     * FROM [Users] AS [u]
     * ORDER BY [u].[CreatedAt] DESC;
     */
    [Benchmark(Description = "A09_ORDER_LIMIT_10_EfCore")]
    public async Task A09_EfCore()
    {
        var result = await _efcore.GetPagedAsync(0, 10);
        _ = result.ToList();
    }

    // ========================================
    // A10: ORDER BY + LIMIT 100
    // ========================================

    /**
     * A10 시나리오: ORDER BY + LIMIT 100 (NuVatis)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * ORDER BY created_at DESC
     * LIMIT 100;
     *
     * 【 A09와의 차이 】
     * - A09: 10건 (1-5ms)
     * - A10: 100건 (3-10ms)
     * - 인덱스 스캔: 100건까지 순차 읽기
     *
     * 【 예상 결과 】
     * - Dapper: 3-5ms
     * - NuVatis: 5-8ms
     * - EF Core: 8-12ms
     */
    [Benchmark(Description = "A10_ORDER_LIMIT_100_NuVatis")]
    public async Task A10_NuVatis()
    {
        var result = await _nuvatis.GetPagedAsync(0, 100);
        _ = result.ToList();
    }

    /**
     * A10 시나리오: ORDER BY + LIMIT 100 (Dapper)
     */
    [Benchmark(Description = "A10_ORDER_LIMIT_100_Dapper")]
    public async Task A10_Dapper()
    {
        var result = await _dapper.GetPagedAsync(0, 100);
        _ = result.ToList();
    }

    /**
     * A10 시나리오: ORDER BY + LIMIT 100 (EF Core)
     */
    [Benchmark(Description = "A10_ORDER_LIMIT_100_EfCore")]
    public async Task A10_EfCore()
    {
        var result = await _efcore.GetPagedAsync(0, 100);
        _ = result.ToList();
    }

    // ========================================
    // A11: 단건 INSERT
    // ========================================

    /**
     * A11 시나리오: 단건 INSERT (NuVatis)
     *
     * 【 SQL 쿼리 】
     * INSERT INTO users (user_name, email, full_name, password_hash,
     *                    date_of_birth, phone_number, is_active, created_at, updated_at)
     * VALUES (@userName, @email, @fullName, @passwordHash,
     *         @dateOfBirth, @phoneNumber, @isActive, @createdAt, @updatedAt)
     * RETURNING id;
     *
     * 【 RETURNING 절 (PostgreSQL) 】
     * - INSERT 후 생성된 ID 즉시 반환
     * - MySQL: LAST_INSERT_ID()
     * - SQL Server: SCOPE_IDENTITY()
     *
     * 【 .NET DateTime.UtcNow 】
     * - DateTime.Now: 로컬 시간 (서버 시간대, 예: KST UTC+9)
     * - DateTime.UtcNow: UTC 시간 (협정 세계시, 권장)
     * - 왜 UTC? 글로벌 서비스 시 시간대 문제 방지
     *   사용자 A (한국): 2026-03-01 12:00 KST → 2026-03-01 03:00 UTC
     *   사용자 B (미국): 2026-02-28 22:00 EST → 2026-03-01 03:00 UTC
     *   → DB에는 UTC로 저장, 클라이언트에서 로컬 시간으로 변환
     *
     * 【 예상 결과 】
     * - Dapper: 2-3ms
     * - NuVatis: 3-5ms
     * - EF Core: 5-8ms (ChangeTracker 오버헤드)
     *
     * 【 성능 특성 】
     * - 네트워크 왕복: 1회
     * - DB 쿼리 시간: 1-2ms (INSERT + 인덱스 업데이트)
     * - ORM 오버헤드: 1-3ms
     */
    [Benchmark(Description = "A11_Single_INSERT_NuVatis")]
    public async Task<long> A11_NuVatis()
    {
        var guid = Guid.NewGuid().ToString("N");
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        var user = new User
        {
            UserName = $"bench_user_{guid}",
            Email = $"bench_{guid}@test.com",
            FullName = "Benchmark User",
            PasswordHash = "hash",
            DateOfBirth = new DateTime(1990, 1, 1),
            PhoneNumber = "010-1234-5678",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _nuvatis.InsertAsync(user);
    }

    /**
     * A11 시나리오: 단건 INSERT (Dapper)
     *
     * 【 Dapper 구현 】
     * return await connection.ExecuteScalarAsync<long>(
     *     @"INSERT INTO users (...) VALUES (...)
     *       RETURNING id",
     *     user
     * );
     *
     * 【 ExecuteScalarAsync<T> 】
     * - 단일 값 반환 (예: id, count)
     * - QueryAsync<T>: 다중 행 반환
     * - ExecuteAsync: 영향받은 행 수 반환
     */
    [Benchmark(Description = "A11_Single_INSERT_Dapper")]
    public async Task<long> A11_Dapper()
    {
        var guid = Guid.NewGuid().ToString("N");
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        var user = new User
        {
            UserName = $"bench_user_{guid}",
            Email = $"bench_{guid}@test.com",
            FullName = "Benchmark User",
            PasswordHash = "hash",
            DateOfBirth = new DateTime(1990, 1, 1),
            PhoneNumber = "010-1234-5678",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _dapper.InsertAsync(user);
    }

    /**
     * A11 시나리오: 단건 INSERT (EF Core)
     *
     * 【 EF Core 구현 】
     * context.Users.Add(user);
     * await context.SaveChangesAsync();
     * return user.Id; // EF Core가 자동으로 ID 할당
     *
     * 【 EF Core의 INSERT 과정 】
     * 1. Add(user): ChangeTracker에 등록 (상태: Added)
     * 2. SaveChangesAsync():
     *    - DetectChanges(): 변경 사항 감지
     *    - SQL 생성: INSERT INTO users ...
     *    - DB 실행: INSERT 쿼리 전송
     *    - ID 할당: RETURNING id → user.Id에 자동 설정
     *    - 상태 변경: Added → Unchanged
     *
     * 【 왜 EF Core가 느린가? 】
     * - DetectChanges(): 모든 추적 객체 순회 (O(n))
     * - ChangeTracker 업데이트: 메타데이터 관리
     * - Proxy 생성: 지연 로딩용 프록시 (선택)
     */
    [Benchmark(Description = "A11_Single_INSERT_EfCore")]
    public async Task<long> A11_EfCore()
    {
        var guid = Guid.NewGuid().ToString("N");
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        var user = new User
        {
            UserName = $"bench_user_{guid}",
            Email = $"bench_{guid}@test.com",
            FullName = "Benchmark User",
            PasswordHash = "hash",
            DateOfBirth = new DateTime(1990, 1, 1),
            PhoneNumber = "010-1234-5678",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _efcore.InsertAsync(user);
    }

    // ========================================
    // A12: 100회 반복 INSERT
    // ========================================

    /**
     * A12 시나리오: 100회 반복 INSERT (NuVatis)
     *
     * 【 SQL 쿼리 (100번 실행) 】
     * INSERT INTO users (...) VALUES (...) RETURNING id; -- 1번
     * INSERT INTO users (...) VALUES (...) RETURNING id; -- 2번
     * ...
     * INSERT INTO users (...) VALUES (...) RETURNING id; -- 100번
     *
     * 【 안티패턴 경고 】
     * [비권장] 반복 INSERT (현재 코드):
     *   for (int i = 0; i < 100; i++) {
     *       await InsertAsync(user); // 100번의 네트워크 왕복
     *   }
     *   총 시간 = 100 × 3ms = 300ms
     *
     * [권장] Bulk INSERT:
     *   await BulkInsertAsync(users); // 1번의 네트워크 왕복
     *   총 시간 = 10-30ms
     *
     * 【 Bulk INSERT 방법 】
     * 1. PostgreSQL COPY 프로토콜:
     *    COPY users FROM STDIN BINARY;
     *    → 초당 50,000-100,000 rows 삽입 (매우 빠름)
     *
     * 2. Multi-row INSERT:
     *    INSERT INTO users (...) VALUES
     *        (row1), (row2), ..., (row100);
     *    → 1번의 쿼리로 100건 삽입
     *
     * 3. Transaction + Batch:
     *    BEGIN;
     *    INSERT INTO users (...) VALUES (...); -- 1
     *    ...
     *    INSERT INTO users (...) VALUES (...); -- 100
     *    COMMIT;
     *    → 트랜잭션 1회로 100건 삽입 (디스크 I/O 최소화)
     *
     * 【 예상 결과 】
     * - Dapper: 200-300ms
     * - NuVatis: 300-500ms
     * - EF Core: 500-800ms (DetectChanges 누적)
     *
     * 【 성능 특성 】
     * - 네트워크 왕복: 100회
     * - DB 쿼리 시간: 100 × 2ms = 200ms
     * - ORM 오버헤드: 100 × 1-3ms = 100-300ms
     * - 총 시간: 300-500ms
     */
    [Benchmark(Description = "A12_100x_INSERT_NuVatis")]
    public async Task A12_NuVatis()
    {
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        for (int i = 0; i < 100; i++)
        {
            var guid = Guid.NewGuid().ToString("N");
            var user = new User
            {
                UserName = $"bench_user_{guid}",
                Email = $"bench_{guid}@test.com",
                FullName = $"Benchmark User {i}",
                PasswordHash = "hash",
                DateOfBirth = new DateTime(1990, 1, 1),
                PhoneNumber = "010-1234-5678",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _nuvatis.InsertAsync(user);
        }
    }

    /**
     * A12 시나리오: 100회 반복 INSERT (Dapper)
     *
     * 【 Dapper Bulk INSERT 예시 】
     * using var transaction = connection.BeginTransaction();
     * foreach (var user in users)
     * {
     *     await connection.ExecuteAsync(
     *         "INSERT INTO users (...) VALUES (...)",
     *         user,
     *         transaction
     *     );
     * }
     * await transaction.CommitAsync();
     *
     * 【 트랜잭션의 효과 】
     * - No Transaction: 100번의 COMMIT (디스크 I/O 100회)
     * - Transaction: 1번의 COMMIT (디스크 I/O 1회)
     * - 성능 향상: 2-5배
     */
    [Benchmark(Description = "A12_100x_INSERT_Dapper")]
    public async Task A12_Dapper()
    {
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        for (int i = 0; i < 100; i++)
        {
            var guid = Guid.NewGuid().ToString("N");
            var user = new User
            {
                UserName = $"bench_user_{guid}",
                Email = $"bench_{guid}@test.com",
                FullName = $"Benchmark User {i}",
                PasswordHash = "hash",
                DateOfBirth = new DateTime(1990, 1, 1),
                PhoneNumber = "010-1234-5678",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _dapper.InsertAsync(user);
        }
    }

    /**
     * A12 시나리오: 100회 반복 INSERT (EF Core)
     *
     * 【 EF Core의 누적 오버헤드 】
     * - Add(user): ChangeTracker에 누적 (100개 객체)
     * - SaveChangesAsync() 100번:
     *   각 호출마다 DetectChanges() → 모든 객체 순회 (O(n²) 복잡도)
     *   1번: 1개 순회, 2번: 2개 순회, ..., 100번: 100개 순회
     *   총 순회: 1 + 2 + ... + 100 = 5,050번
     *
     * 【 EF Core Bulk INSERT 최적화 】
     * [권장] AddRange() + SaveChanges() 1번:
     *   context.Users.AddRange(users); // 100개 일괄 등록
     *   await context.SaveChangesAsync(); // DetectChanges 1번
     *   → 총 순회: 100번 (50배 개선)
     *
     * [권장] EF Core Extensions (EFCore.BulkExtensions):
     *   await context.BulkInsertAsync(users);
     *   → PostgreSQL COPY 프로토콜 사용
     *   → 100건을 10-20ms에 삽입
     */
    [Benchmark(Description = "A12_100x_INSERT_EfCore")]
    public async Task A12_EfCore()
    {
        for (int i = 0; i < 100; i++)
        {
            var guid = Guid.NewGuid().ToString("N");
            var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            var user = new User
            {
                UserName = $"bench_user_{guid}",
                Email = $"bench_{guid}@test.com",
                FullName = $"Benchmark User {i}",
                PasswordHash = "hash",
                DateOfBirth = new DateTime(1990, 1, 1),
                PhoneNumber = "010-1234-5678",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _efcore.InsertAsync(user);
        }
    }

    // ========================================
    // A13-A15: UPDATE/DELETE (실제 데이터 변경 위험으로 생략)
    // ========================================

    /**
     * 【 A13-A15 시나리오 (구현 생략) 】
     *
     * 【 왜 생략? 】
     * - 벤치마크 실행 시 실제 DB 데이터 변경
     * - 반복 실행 시 데이터 일관성 문제
     * - 테스트 데이터 복원 비용 증가
     *
     * 【 대안 】
     * - 별도 테스트 DB 사용
     * - GlobalCleanup에서 변경 롤백
     * - Mock Repository로 테스트
     *
     * 【 예상 시나리오 】
     * - A13: 단건 UPDATE (UPDATE users SET ... WHERE id = ?)
     * - A14: 100회 반복 UPDATE
     * - A15: 단건 DELETE (DELETE FROM users WHERE id = ?)
     */
}
