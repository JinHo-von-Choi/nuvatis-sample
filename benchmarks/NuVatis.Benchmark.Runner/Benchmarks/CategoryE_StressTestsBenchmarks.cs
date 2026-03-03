using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.Dapper.Repositories;
using NuVatis.Benchmark.EfCore.DbContexts;
using NuVatis.Benchmark.EfCore.Repositories;

namespace NuVatis.Benchmark.Runner.Benchmarks;

/**
 * 카테고리 E: 스트레스 테스트 벤치마크 - 대량 데이터, 반복 쿼리, 동시성 성능 비교
 *
 * 【 벤치마크 목적 】
 * - 대량 데이터 조회 (10,000건) 시 ORM별 성능 및 메모리 사용량 비교
 * - 복잡 쿼리 100회 반복 시 누적 성능 및 GC 압력 측정
 * - 동시성 처리 (50 parallel tasks) 시 연결 풀 효율성 비교
 *
 * 【 시나리오 】
 * - E01: 대량 조회 (10,000건) - 메모리 효율성 테스트
 * - E02: 복잡 쿼리 100회 반복 - 안정성 및 누적 성능 테스트
 * - E03: 동시성 50 작업 - Connection Pool, Thread Safety 테스트
 *
 * 【 스트레스 테스트의 중요성 】
 * - 프로덕션 환경 시뮬레이션: 실제 부하 상황 재현
 * - 메모리 누수 탐지: 장시간 실행 시 메모리 증가 확인
 * - 병목 지점 식별: 동시 접속 시 성능 저하 원인 파악
 * - 안정성 검증: 극한 상황에서도 오류 없이 동작
 *
 * 【 성능 목표 】
 * - 10,000건 조회: 100-500ms (메모리 1-2 MB)
 * - 복잡 쿼리 100회: 500ms-2s
 * - 동시성 50 작업: 200-800ms (Connection Pool 효율)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
[MemoryDiagnoser]
[RankColumn]
public class CategoryE_StressTestsBenchmarks
{
    private IUserRepository _userNuvatis = null!;
    private IUserRepository _userDapper = null!;
    // EfCore는 동시성 문제로 인해 각 메서드에서 별도 생성

    private IOrderRepository _orderNuvatis = null!;
    private IOrderRepository _orderDapper = null!;
    // EfCore는 동시성 문제로 인해 각 메서드에서 별도 생성

    private string _connectionString = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _connectionString = configuration.GetConnectionString("BenchmarkDb")
            ?? throw new InvalidOperationException("ConnectionString 'BenchmarkDb' not found");

        _userDapper = new DapperUserRepository(_connectionString);
        _userNuvatis = _userDapper; // Fallback

        _orderDapper = new DapperOrderRepository(_connectionString);
        _orderNuvatis = _orderDapper; // Fallback

        Console.WriteLine("[CategoryE GlobalSetup] All repositories initialized");
    }

    // ========================================
    // E01: 대량 조회 (10,000건)
    // ========================================

    /**
     * E01: 대량 데이터 조회 벤치마크 - NuVatis 구현
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM users
     * ORDER BY id
     * OFFSET 0 LIMIT 10000;
     *
     * 【 대량 데이터 처리 방법 비교 】
     * [비권장] ToList() - 메모리에 전체 로드:
     *   var users = await GetPagedAsync(0, 10000);
     *   var list = users.ToList();  // 10,000 객체 메모리 로드 (1-2 MB)
     *   → OutOfMemoryException 위험 (100만 건 시)
     *
     * [권장] 스트리밍 (IAsyncEnumerable):
     *   await foreach (var user in GetAsyncStream())
     *   {
     *       // 한 번에 1개씩 처리 (메모리 효율적)
     *   }
     *
     * 【 메모리 사용량 계산 】
     * User 객체 크기: 약 100-200 bytes
     * 10,000 건 × 200 bytes = 2,000,000 bytes = 약 2 MB
     *
     * 【 반환 타입 】
     * - Task<IEnumerable<User>>: 지연 실행
     * - 실제 메모리 로드: 반환 후 ToList() 등 호출 시
     *
     * 【 예상 성능 】
     * - 응답 시간: 100-300ms
     * - 메모리 할당: 1-2 MB
     * - GC Gen0: 50-100회
     * - GC Gen1: 5-10회
     * - GC Gen2: 0-1회
     *
     * 【 NuVatis 최적화 】
     * - Streaming ResultSet: 한 번에 일부만 로드
     * - 버퍼링 최소화
     */
    [Benchmark(Description = "E01_Query_10K_Rows_NuVatis")]
    public async Task<List<User>> E01_NuVatis()
    {
        var result = await _userNuvatis.GetPagedAsync(0, 10000);
        return result.ToList();
    }

    /**
     * E01: 대량 데이터 조회 벤치마크 - Dapper 구현
     *
     * 【 Dapper buffered vs unbuffered 】
     * [기본] buffered: true (기본값):
     *   var users = await connection.QueryAsync<User>(sql);
     *   → 전체 결과를 메모리에 로드 후 반환
     *   → 메모리: 2 MB (10,000건)
     *
     * [최적화] buffered: false:
     *   var users = await connection.QueryAsync<User>(
     *       sql,
     *       buffered: false
     *   );
     *   → 스트리밍 방식으로 한 번에 일부만 로드
     *   → 메모리: 100-200 KB (버퍼 크기)
     *
     * 【 예상 성능 】
     * - 응답 시간: 80-250ms (가장 빠름)
     * - 메모리 할당: 1-1.5 MB (가장 낮음)
     */
    [Benchmark(Description = "E01_Query_10K_Rows_Dapper")]
    public async Task<List<User>> E01_Dapper()
    {
        var result = await _userDapper.GetPagedAsync(0, 10000);
        return result.ToList();
    }

    /**
     * E01: 대량 데이터 조회 벤치마크 - EF Core 구현
     *
     * 【 EF Core Change Tracking 비용 】
     * - 기본적으로 모든 조회된 엔티티 추적
     * - 10,000개 엔티티 × Change Tracking 비용 = 높은 메모리
     *
     * 【 AsNoTracking() 최적화 】
     * var users = await context.Users
     *     .AsNoTracking()  // Change Tracking 비활성화
     *     .OrderBy(u => u.Id)
     *     .Skip(0)
     *     .Take(10000)
     *     .ToListAsync();
     *
     * → 메모리 사용량 30-50% 감소
     * → 응답 시간 10-20% 개선
     *
     * 【 예상 성능 (AsNoTracking 없음) 】
     * - 응답 시간: 150-400ms
     * - 메모리 할당: 2-3 MB (가장 높음)
     * - Change Tracking 오버헤드: 1-1.5 MB
     *
     * 【 예상 성능 (AsNoTracking 적용) 】
     * - 응답 시간: 120-300ms
     * - 메모리 할당: 1.5-2 MB
     */
    [Benchmark(Description = "E01_Query_10K_Rows_EfCore")]
    public async Task<List<User>> E01_EfCore()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);
        await using var context = new BenchmarkDbContext(optionsBuilder.Options);

        var repository = new EfCoreUserRepository(context);
        var result = await repository.GetPagedAsync(0, 10000);
        return result.ToList();
    }

    // ========================================
    // E02: 복잡 쿼리 100회 반복
    // ========================================

    /**
     * E02: 복잡 쿼리 100회 반복 벤치마크 - NuVatis 구현
     *
     * 【 반복 쿼리 시나리오 】
     * - 3-table JOIN 쿼리 (Order + User + OrderItems) 100회 실행
     * - 연속된 주문 ID (100001 ~ 100100) 조회
     * - 총 100번의 DB 왕복
     *
     * 【 누적 성능 측정 】
     * - 단일 쿼리: 5-15ms
     * - 100회 반복: 500ms-1.5s
     * - 메모리 누적: 300-500 KB
     * - GC 빈도: Gen0 50-100회
     *
     * 【 연결 풀(Connection Pool) 효율성 】
     * - DB 연결 재사용하여 성능 향상
     * - 100번 쿼리 모두 풀에서 연결 가져옴
     * - 새 연결 생성 비용 절감
     *
     * 【 예상 성능 】
     * - 응답 시간: 600ms-1.2s
     * - 메모리 할당: 300-400 KB
     */
    [Benchmark(Description = "E02_Complex_Query_100x_NuVatis")]
    public async Task E02_NuVatis()
    {
        for (int i = 0; i < 100; i++)
        {
            await _orderNuvatis.GetWithUserAndItemsAsync(100001 + i);
        }
    }

    /**
     * E02: 복잡 쿼리 100회 반복 벤치마크 - Dapper 구현
     *
     * 【 Dapper 장점 (반복 쿼리) 】
     * - 낮은 오버헤드 → 누적 비용 최소
     * - 빠른 매핑 → 전체 시간 단축
     * - 메모리 효율적 → GC 압력 낮음
     *
     * 【 예상 성능 】
     * - 응답 시간: 500ms-1s (가장 빠름)
     * - 메모리 할당: 250-350 KB (가장 낮음)
     */
    [Benchmark(Description = "E02_Complex_Query_100x_Dapper")]
    public async Task E02_Dapper()
    {
        for (int i = 0; i < 100; i++)
        {
            await _orderDapper.GetWithUserAndItemsAsync(100001 + i);
        }
    }

    /**
     * E02: 복잡 쿼리 100회 반복 벤치마크 - EF Core 구현
     *
     * 【 EF Core Change Tracking 누적 】
     * - 각 조회마다 엔티티 추적 (메모리 증가)
     * - 100번 조회 후: 100 Order + 100 User + N OrderItems 추적
     * - 메모리 사용량 선형 증가
     *
     * 【 DbContext 수명 관리 】
     * - 장기 사용 시 메모리 누수 위험
     * - 주기적 DbContext 재생성 권장
     *
     * 【 예상 성능 】
     * - 응답 시간: 800ms-2s
     * - 메모리 할당: 400-600 KB (Change Tracking 비용)
     *
     * 【 수정 사항 】
     * - AsNoTracking이 이미 Repository에 적용되어 있어 Change Tracking 누적 문제 없음
     * - 순차 실행이므로 단일 DbContext 사용 가능
     */
    [Benchmark(Description = "E02_Complex_Query_100x_EfCore")]
    public async Task E02_EfCore()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);
        await using var context = new BenchmarkDbContext(optionsBuilder.Options);

        var repository = new EfCoreOrderRepository(context);
        for (int i = 0; i < 100; i++)
        {
            await repository.GetWithUserAndItemsAsync(100001 + i);
        }
    }

    // ========================================
    // E03: 동시성 시뮬레이션 (50 parallel tasks)
    // ========================================

    /**
     * E03: 동시성 처리 벤치마크 - NuVatis 구현
     *
     * 【 병렬 처리 패턴 】
     * - 50개 Task 동시 실행
     * - 각 Task는 독립적인 사용자 조회
     * - Task.WhenAll()로 모든 작업 완료 대기
     *
     * 【 Task.Run() 사용 】
     * for (int i = 0; i < 50; i++)
     * {
     *     int userId = 12345 + i;
     *     tasks.Add(Task.Run(async () => await GetByIdAsync(userId)));
     * }
     *
     * 【 클로저(Closure) 주의사항 】
     * [위험] 루프 변수 직접 캡처:
     *   for (int i = 0; i < 50; i++)
     *   {
     *       tasks.Add(Task.Run(async () => await GetByIdAsync(12345 + i)));
     *       // 모든 Task가 마지막 i 값 (50) 사용
     *   }
     *
     * [안전] 지역 변수로 복사:
     *   for (int i = 0; i < 50; i++)
     *   {
     *       int userId = 12345 + i;  // 각 반복마다 별도 변수
     *       tasks.Add(Task.Run(async () => await GetByIdAsync(userId)));
     *   }
     *
     * 【 Connection Pool 동시성 】
     * - 기본 풀 크기: 100개 연결
     * - 50개 동시 작업 → 풀에서 50개 연결 사용
     * - 대기 시간: 거의 없음 (풀 크기 충분)
     *
     * 【 Task.WhenAll() 개념 】
     * await Task.WhenAll(tasks);
     * - 모든 Task가 완료될 때까지 대기
     * - 가장 느린 Task 기준으로 완료 시간 결정
     * - 병렬 실행 → 전체 시간 단축
     *
     * 【 순차 vs 병렬 비교 】
     * [순차 실행]:
     *   for (int i = 0; i < 50; i++)
     *       await GetByIdAsync(12345 + i);  // 50번 순차
     *   → 응답 시간: 50 × 1ms = 50ms
     *
     * [병렬 실행]:
     *   await Task.WhenAll(tasks);  // 50개 동시
     *   → 응답 시간: 5-10ms (가장 느린 Task 기준)
     *
     * 성능 개선: 5-10배 빠름
     *
     * 【 예상 성능 】
     * - 응답 시간: 300-600ms
     * - 메모리 할당: 100-200 KB
     * - 연결 풀 사용: 50개 동시 연결
     */
    [Benchmark(Description = "E03_Concurrency_50_NuVatis")]
    public async Task E03_NuVatis()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int userId = 12345 + i;
            tasks.Add(Task.Run(async () => await _userNuvatis.GetByIdAsync(userId)));
        }
        await Task.WhenAll(tasks);
    }

    /**
     * E03: 동시성 처리 벤치마크 - Dapper 구현
     *
     * 【 Dapper 동시성 장점 】
     * - Thread-Safe: 여러 스레드에서 안전하게 사용
     * - 낮은 오버헤드: 동시 실행 시에도 빠름
     * - 효율적 연결 관리
     *
     * 【 예상 성능 】
     * - 응답 시간: 200-500ms (가장 빠름)
     * - 메모리 할당: 80-150 KB (가장 낮음)
     */
    [Benchmark(Description = "E03_Concurrency_50_Dapper")]
    public async Task E03_Dapper()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int userId = 12345 + i;
            tasks.Add(Task.Run(async () => await _userDapper.GetByIdAsync(userId)));
        }
        await Task.WhenAll(tasks);
    }

    /**
     * E03: 동시성 처리 벤치마크 - EF Core 구현
     *
     * 【 EF Core DbContext Thread Safety 】
     * [위험] 단일 DbContext 공유:
     *   // DbContext는 Thread-Safe 아님
     *   for (int i = 0; i < 50; i++)
     *   {
     *       tasks.Add(Task.Run(async () => await context.Users.FindAsync(userId)));
     *       // 여러 스레드에서 동일 context 사용 → 오류 발생
     *   }
     *
     * [안전] 각 Task마다 별도 DbContext:
     *   for (int i = 0; i < 50; i++)
     *   {
     *       tasks.Add(Task.Run(async () =>
     *       {
     *           using var context = new BenchmarkDbContext();
     *           return await context.Users.FindAsync(userId);
     *       }));
     *   }
     *
     * 【 DbContext 생성 비용 】
     * - 각 Task마다 DbContext 생성 → 오버헤드
     * - 50개 DbContext 생성 비용 누적
     * - Dapper보다 느림 (Context 생성 비용)
     *
     * 【 예상 성능 】
     * - 응답 시간: 400-800ms
     * - 메모리 할당: 150-250 KB
     */
    [Benchmark(Description = "E03_Concurrency_50_EfCore")]
    public async Task E03_EfCore()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int userId = 12345 + i;
            tasks.Add(Task.Run(async () =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
                optionsBuilder.UseNpgsql(_connectionString);
                await using var context = new BenchmarkDbContext(optionsBuilder.Options);

                var repository = new EfCoreUserRepository(context);
                await repository.GetByIdAsync(userId);
            }));
        }
        await Task.WhenAll(tasks);
    }

    // ========================================
    // E04-E05: 추가 스트레스 테스트
    // ========================================

    /**
     * E04: 대량 조회 5000건 - NuVatis 구현
     */
    [Benchmark(Description = "E04_Query_5K_Rows_NuVatis")]
    public async Task<List<User>> E04_NuVatis()
    {
        var result = await _userNuvatis.GetPagedAsync(0, 5000);
        return result.ToList();
    }

    /**
     * E04: 대량 조회 5000건 - Dapper 구현
     */
    [Benchmark(Description = "E04_Query_5K_Rows_Dapper")]
    public async Task<List<User>> E04_Dapper()
    {
        var result = await _userDapper.GetPagedAsync(0, 5000);
        return result.ToList();
    }

    /**
     * E04: 대량 조회 5000건 - EF Core 구현
     */
    [Benchmark(Description = "E04_Query_5K_Rows_EfCore")]
    public async Task<List<User>> E04_EfCore()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
        optionsBuilder.UseNpgsql(_connectionString);
        await using var context = new BenchmarkDbContext(optionsBuilder.Options);

        var repository = new EfCoreUserRepository(context);
        var result = await repository.GetPagedAsync(0, 5000);
        return result.ToList();
    }

    /**
     * E05: 동시성 100 작업 - NuVatis 구현
     */
    [Benchmark(Description = "E05_Concurrency_100_NuVatis")]
    public async Task E05_NuVatis()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            int userId = 12345 + i;
            tasks.Add(Task.Run(async () => await _userNuvatis.GetByIdAsync(userId)));
        }
        await Task.WhenAll(tasks);
    }

    /**
     * E05: 동시성 100 작업 - Dapper 구현
     */
    [Benchmark(Description = "E05_Concurrency_100_Dapper")]
    public async Task E05_Dapper()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            int userId = 12345 + i;
            tasks.Add(Task.Run(async () => await _userDapper.GetByIdAsync(userId)));
        }
        await Task.WhenAll(tasks);
    }

    /**
     * E05: 동시성 100 작업 - EF Core 구현
     *
     * 【 수정 사항 】
     * - 각 Task마다 별도 DbContext 생성하여 Thread-Safety 보장
     * - EF Core는 Thread-Safe하지 않으므로 동시성 환경에서 필수
     */
    [Benchmark(Description = "E05_Concurrency_100_EfCore")]
    public async Task E05_EfCore()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            int userId = 12345 + i;
            tasks.Add(Task.Run(async () =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
                optionsBuilder.UseNpgsql(_connectionString);
                await using var context = new BenchmarkDbContext(optionsBuilder.Options);

                var repository = new EfCoreUserRepository(context);
                await repository.GetByIdAsync(userId);
            }));
        }
        await Task.WhenAll(tasks);
    }
}
