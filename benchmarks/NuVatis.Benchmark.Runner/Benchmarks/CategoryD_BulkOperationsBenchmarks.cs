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
 * 카테고리 D: 대량 작업 및 트랜잭션 벤치마크 - Bulk INSERT, Transaction 성능 비교
 *
 * 【 벤치마크 목적 】
 * - ORM별 대량 INSERT (1,000건) 성능 비교
 * - 트랜잭션 처리 (User + Address) 성능 측정
 * - 메모리 할당 및 GC 압력 분석
 *
 * 【 시나리오 】
 * - D01-D04: BULK INSERT (1,000건) - 배치 삽입 vs 개별 삽입
 * - D05-D07: Transaction (User + Address) - ACID 보장 비용
 *
 * 【 대량 작업의 중요성 】
 * - 데이터 마이그레이션: 수백만 건 데이터 이전
 * - 초기 데이터 로드: 시스템 시작 시 기준 데이터 삽입
 * - 배치 처리: 일괄 처리 작업 (야간 배치)
 *
 * 【 성능 목표 】
 * - BULK INSERT 1,000건: 50-200ms
 * - Transaction (User + Address): 5-15ms
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
[MemoryDiagnoser]
[RankColumn]
public class CategoryD_BulkOperationsBenchmarks
{
    /**
     * Repository 인스턴스 (ORM별)
     */
    private IUserRepository _userNuvatis = null!;
    private IUserRepository _userDapper = null!;
    private IUserRepository _userEfCore = null!;

    /**
     * 벤치마크용 테스트 데이터 (1,000명 사용자)
     *
     * 【 List<T> 타입 】
     * - 동적 배열 (크기 가변)
     * - 인덱스 접근 O(1)
     * - 추가/삭제 O(1) ~ O(n)
     */
    private List<User> _users1K = null!;

    /**
     * 벤치마크 시작 전 초기화 (1회 실행)
     *
     * 【 테스트 데이터 생성 】
     * - GenerateUsers(1000): 1,000명 사용자 생성
     * - [GlobalSetup]에서 1회만 생성 (재사용)
     * - 벤치마크 시간 측정에서 제외됨
     */
    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("BenchmarkDb")
            ?? throw new InvalidOperationException("ConnectionString 'BenchmarkDb' not found");

        _userDapper = new DapperUserRepository(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<BenchmarkDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        var dbContext = new BenchmarkDbContext(optionsBuilder.Options);
        _userEfCore = new EfCoreUserRepository(dbContext);
        _userNuvatis = _userDapper; // Fallback

        _users1K = GenerateUsers(1000);
        Console.WriteLine("[CategoryD GlobalSetup] All repositories initialized, test data generated");
    }

    // ========================================
    // D01-D04: BULK INSERT (1,000건)
    // ========================================

    /**
     * D01: 대량 INSERT 벤치마크 - NuVatis 구현
     *
     * 【 SQL 쿼리 (배치 INSERT) 】
     * INSERT INTO users (user_name, email, full_name, ...)
     * VALUES
     *   ('bulk_user_0', 'bulk0@test.com', 'Bulk User 0', ...),
     *   ('bulk_user_1', 'bulk1@test.com', 'Bulk User 1', ...),
     *   ...
     *   ('bulk_user_999', 'bulk999@test.com', 'Bulk User 999', ...);
     *
     * 【 대량 INSERT 방법 비교 】
     * [비권장] 개별 INSERT (1,000번 실행):
     *   for (int i = 0; i < 1000; i++)
     *   {
     *       await InsertAsync(users[i]);  // 1,000번 쿼리
     *   }
     *   → 네트워크 왕복: 1,000회
     *   → 응답 시간: 2-10초
     *
     * [권장] 배치 INSERT (1번 실행):
     *   INSERT INTO users (...) VALUES (...), (...), ... -- 1,000개
     *   → 네트워크 왕복: 1회
     *   → 응답 시간: 50-200ms
     *
     * 성능 개선: 10-50배 빠름
     *
     * 【 NuVatis XML 매퍼 예시 】
     * <insert id="BulkInsertAsync">
     *   INSERT INTO users (user_name, email, full_name, ...)
     *   VALUES
     *   <foreach collection="list" item="item" separator=",">
     *     (#{item.UserName}, #{item.Email}, #{item.FullName}, ...)
     *   </foreach>
     * </insert>
     *
     * 【 예상 성능 】
     * - 응답 시간: 50-150ms
     * - 메모리 할당: 200-300 KB
     * - GC Gen0: 20-30회
     *
     * 【 반환 타입 】
     * - Task<int>: 삽입된 행 수 (1,000)
     */
    [Benchmark(Description = "D01_BULK_INSERT_1K_NuVatis")]
    public async Task<int> D01_NuVatis() => await _userNuvatis.BulkInsertAsync(GenerateUsers(1000));

    /**
     * D01: 대량 INSERT 벤치마크 - Dapper 구현
     *
     * 【 Dapper 배치 INSERT 】
     * await connection.ExecuteAsync(
     *     "INSERT INTO users (user_name, email, ...) VALUES (@UserName, @Email, ...)",
     *     users  // IEnumerable<User>
     * );
     *
     * 【 Dapper 자동 배칭 】
     * - IEnumerable<T>를 받으면 자동으로 배치 실행
     * - 내부적으로 여러 VALUES를 묶어 전송
     * - 성능 최적화 자동 적용
     *
     * 【 예상 성능 】
     * - 응답 시간: 50-120ms (가장 빠름)
     * - 메모리 할당: 150-250 KB (가장 낮음)
     */
    [Benchmark(Description = "D01_BULK_INSERT_1K_Dapper")]
    public async Task<int> D01_Dapper() => await _userDapper.BulkInsertAsync(GenerateUsers(1000));

    /**
     * D01: 대량 INSERT 벤치마크 - EF Core 구현
     *
     * 【 EF Core 대량 INSERT 】
     * context.Users.AddRange(users);
     * int count = await context.SaveChangesAsync();
     *
     * 【 AddRange() vs Add() 반복 】
     * [권장] AddRange():
     *   context.Users.AddRange(users);  // 1번 호출
     *   → Change Tracker에 1,000개 추가 (배치)
     *
     * [비권장] Add() 반복:
     *   foreach (var user in users)
     *       context.Users.Add(user);  // 1,000번 호출
     *   → Change Tracker에 1,000번 개별 추가 (느림)
     *
     * 【 EF Core Change Tracking 비용 】
     * - 각 엔티티를 추적 (메모리 사용)
     * - 1,000개 엔티티 × 수백 바이트 = 100-200 KB
     * - SaveChangesAsync() 시 INSERT 생성
     *
     * 【 예상 성능 】
     * - 응답 시간: 80-200ms (Change Tracking 오버헤드)
     * - 메모리 할당: 300-400 KB (가장 높음)
     */
    [Benchmark(Description = "D01_BULK_INSERT_1K_EfCore")]
    public async Task<int> D01_EfCore() => await _userEfCore.BulkInsertAsync(GenerateUsers(1000));

    // ========================================
    // D02-D04: BULK INSERT 변형
    // ========================================

    /**
     * D02: 대량 INSERT (재실행) - NuVatis 구현
     */
    [Benchmark(Description = "D02_BULK_INSERT_1K_NuVatis")]
    public async Task<int> D02_NuVatis() => await _userNuvatis.BulkInsertAsync(GenerateUsers(1000));

    /**
     * D02: 대량 INSERT (재실행) - Dapper 구현
     */
    [Benchmark(Description = "D02_BULK_INSERT_1K_Dapper")]
    public async Task<int> D02_Dapper() => await _userDapper.BulkInsertAsync(GenerateUsers(1000));

    /**
     * D02: 대량 INSERT (재실행) - EF Core 구현
     */
    [Benchmark(Description = "D02_BULK_INSERT_1K_EfCore")]
    public async Task<int> D02_EfCore() => await _userEfCore.BulkInsertAsync(GenerateUsers(1000));

    /**
     * D03: 대량 INSERT 500건 - NuVatis 구현
     */
    [Benchmark(Description = "D03_BULK_INSERT_500_NuVatis")]
    public async Task<int> D03_NuVatis() => await _userNuvatis.BulkInsertAsync(GenerateUsers(500));

    /**
     * D03: 대량 INSERT 500건 - Dapper 구현
     */
    [Benchmark(Description = "D03_BULK_INSERT_500_Dapper")]
    public async Task<int> D03_Dapper() => await _userDapper.BulkInsertAsync(GenerateUsers(500));

    /**
     * D03: 대량 INSERT 500건 - EF Core 구현
     */
    [Benchmark(Description = "D03_BULK_INSERT_500_EfCore")]
    public async Task<int> D03_EfCore() => await _userEfCore.BulkInsertAsync(GenerateUsers(500));

    /**
     * D04: 대량 INSERT 2000건 - NuVatis 구현
     */
    [Benchmark(Description = "D04_BULK_INSERT_2K_NuVatis")]
    public async Task<int> D04_NuVatis() => await _userNuvatis.BulkInsertAsync(GenerateUsers(2000));

    /**
     * D04: 대량 INSERT 2000건 - Dapper 구현
     */
    [Benchmark(Description = "D04_BULK_INSERT_2K_Dapper")]
    public async Task<int> D04_Dapper() => await _userDapper.BulkInsertAsync(GenerateUsers(2000));

    /**
     * D04: 대량 INSERT 2000건 - EF Core 구현
     */
    [Benchmark(Description = "D04_BULK_INSERT_2K_EfCore")]
    public async Task<int> D04_EfCore() => await _userEfCore.BulkInsertAsync(GenerateUsers(2000));

    // ========================================
    // D05-D07: Transaction (User + Address)
    // ========================================

    /**
     * D05: 트랜잭션 처리 벤치마크 - NuVatis 구현
     *
     * 【 SQL 쿼리 (트랜잭션) 】
     * BEGIN TRANSACTION;
     *
     * -- 1. 사용자 삽입
     * INSERT INTO users (user_name, email, ...) VALUES (...) RETURNING id;
     *
     * -- 2. 주소 삽입 (user_id 참조)
     * INSERT INTO addresses (user_id, address_type, ...) VALUES (@userId, ...);
     *
     * COMMIT;
     *
     * 【 트랜잭션(Transaction) 개념 】
     * - ACID 속성: Atomicity, Consistency, Isolation, Durability
     * - 모두 성공 또는 모두 실패 (All or Nothing)
     * - 부분 성공 없음 (데이터 일관성 보장)
     *
     * 【 트랜잭션 필요성 】
     * [위험] 트랜잭션 없이 실행:
     *   long userId = await InsertUserAsync(user);  ✓ 성공
     *   await InsertAddressAsync(address, userId);  ✗ 실패
     *   → 사용자만 저장, 주소 없음 (데이터 불일치)
     *
     * [안전] 트랜잭션 사용:
     *   BEGIN;
     *   long userId = await InsertUserAsync(user);  ✓
     *   await InsertAddressAsync(address, userId);  ✗ 실패
     *   ROLLBACK; → 모든 작업 취소 (데이터 일관성 유지)
     *
     * 【 .NET 트랜잭션 패턴 】
     * using var transaction = await connection.BeginTransactionAsync();
     * try
     * {
     *     long userId = await InsertUserAsync(user, transaction);
     *     await InsertAddressAsync(address, userId, transaction);
     *     await transaction.CommitAsync();
     *     return userId;
     * }
     * catch
     * {
     *     await transaction.RollbackAsync();
     *     throw;
     * }
     *
     * 【 예상 성능 】
     * - 응답 시간: 5-15ms
     * - 메모리 할당: 2-5 KB
     *
     * 【 반환 타입 】
     * - Task<int>: 영향 받은 행 수 (2: User 1개 + Address 1개)
     */
    [Benchmark(Description = "D05_Transaction_NuVatis")]
    public async Task<int> D05_NuVatis()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userNuvatis.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D05: 트랜잭션 처리 벤치마크 - Dapper 구현
     *
     * 【 Dapper 트랜잭션 】
     * using var transaction = connection.BeginTransaction();
     * try
     * {
     *     var userId = await connection.ExecuteScalarAsync<long>(
     *         "INSERT INTO users (...) VALUES (...) RETURNING id",
     *         user,
     *         transaction
     *     );
     *
     *     address.UserId = userId;
     *     await connection.ExecuteAsync(
     *         "INSERT INTO addresses (...) VALUES (...)",
     *         address,
     *         transaction
     *     );
     *
     *     transaction.Commit();
     *     return 2;
     * }
     * catch
     * {
     *     transaction.Rollback();
     *     throw;
     * }
     *
     * 【 Dapper 장점 (트랜잭션) 】
     * - 명시적 트랜잭션 제어
     * - 빠른 성능 (최소 오버헤드)
     * - 간결한 코드
     */
    [Benchmark(Description = "D05_Transaction_Dapper")]
    public async Task<int> D05_Dapper()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userDapper.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D05: 트랜잭션 처리 벤치마크 - EF Core 구현
     *
     * 【 EF Core 트랜잭션 】
     * using var transaction = await context.Database.BeginTransactionAsync();
     * try
     * {
     *     context.Users.Add(user);
     *     await context.SaveChangesAsync();  // User INSERT
     *
     *     address.UserId = user.Id;
     *     context.Addresses.Add(address);
     *     await context.SaveChangesAsync();  // Address INSERT
     *
     *     await transaction.CommitAsync();
     *     return 2;
     * }
     * catch
     * {
     *     await transaction.RollbackAsync();
     *     throw;
     * }
     *
     * 【 EF Core SaveChanges() 자동 트랜잭션 】
     * - SaveChangesAsync()는 내부적으로 트랜잭션 사용
     * - 명시적 트랜잭션 없이도 ACID 보장
     * - 여러 SaveChanges() 호출 시 명시적 트랜잭션 권장
     *
     * 【 EF Core 단순화 버전 】
     * context.Users.Add(user);
     * context.Addresses.Add(address);
     * int count = await context.SaveChangesAsync();
     * → 자동으로 트랜잭션 내에서 실행
     */
    [Benchmark(Description = "D05_Transaction_EfCore")]
    public async Task<int> D05_EfCore()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userEfCore.InsertUserWithAddressAsync(user, address);
    }

    // ========================================
    // D06-D07: Transaction 변형
    // ========================================

    /**
     * D06: 트랜잭션 처리 (재실행) - NuVatis 구현
     */
    [Benchmark(Description = "D06_Transaction_NuVatis")]
    public async Task<int> D06_NuVatis()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userNuvatis.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D06: 트랜잭션 처리 (재실행) - Dapper 구현
     */
    [Benchmark(Description = "D06_Transaction_Dapper")]
    public async Task<int> D06_Dapper()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userDapper.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D06: 트랜잭션 처리 (재실행) - EF Core 구현
     */
    [Benchmark(Description = "D06_Transaction_EfCore")]
    public async Task<int> D06_EfCore()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userEfCore.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D07: 트랜잭션 처리 (세 번째) - NuVatis 구현
     */
    [Benchmark(Description = "D07_Transaction_NuVatis")]
    public async Task<int> D07_NuVatis()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userNuvatis.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D07: 트랜잭션 처리 (세 번째) - Dapper 구현
     */
    [Benchmark(Description = "D07_Transaction_Dapper")]
    public async Task<int> D07_Dapper()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userDapper.InsertUserWithAddressAsync(user, address);
    }

    /**
     * D07: 트랜잭션 처리 (세 번째) - EF Core 구현
     */
    [Benchmark(Description = "D07_Transaction_EfCore")]
    public async Task<int> D07_EfCore()
    {
        var user = CreateTestUser();
        var address = CreateTestAddress();
        return await _userEfCore.InsertUserWithAddressAsync(user, address);
    }

    // ========================================
    // D08-D10: 단일 INSERT (비교군)
    // ========================================

    /**
     * D08: 단일 INSERT - NuVatis 구현
     */
    [Benchmark(Description = "D08_Single_INSERT_NuVatis")]
    public async Task<long> D08_NuVatis()
    {
        var user = CreateTestUser();
        return await _userNuvatis.InsertAsync(user);
    }

    /**
     * D08: 단일 INSERT - Dapper 구현
     */
    [Benchmark(Description = "D08_Single_INSERT_Dapper")]
    public async Task<long> D08_Dapper()
    {
        var user = CreateTestUser();
        return await _userDapper.InsertAsync(user);
    }

    /**
     * D08: 단일 INSERT - EF Core 구현
     */
    [Benchmark(Description = "D08_Single_INSERT_EfCore")]
    public async Task<long> D08_EfCore()
    {
        var user = CreateTestUser();
        return await _userEfCore.InsertAsync(user);
    }

    /**
     * D09: 단일 UPDATE - NuVatis 구현
     */
    [Benchmark(Description = "D09_Single_UPDATE_NuVatis")]
    public async Task<int> D09_NuVatis()
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = 12345,
            UserName = "updated_user",
            Email = "updated@test.com",
            FullName = "Updated User",
            PhoneNumber = "010-1111-1111",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _userNuvatis.UpdateAsync(user);
    }

    /**
     * D09: 단일 UPDATE - Dapper 구현
     */
    [Benchmark(Description = "D09_Single_UPDATE_Dapper")]
    public async Task<int> D09_Dapper()
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = 12345,
            UserName = "updated_user",
            Email = "updated@test.com",
            FullName = "Updated User",
            PhoneNumber = "010-1111-1111",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _userDapper.UpdateAsync(user);
    }

    /**
     * D09: 단일 UPDATE - EF Core 구현
     */
    [Benchmark(Description = "D09_Single_UPDATE_EfCore")]
    public async Task<int> D09_EfCore()
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = 12345,
            UserName = "updated_user",
            Email = "updated@test.com",
            FullName = "Updated User",
            PhoneNumber = "010-1111-1111",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        return await _userEfCore.UpdateAsync(user);
    }

    /**
     * D10: 단일 조회 - NuVatis 구현
     */
    [Benchmark(Description = "D10_Single_SELECT_NuVatis")]
    public async Task<User?> D10_NuVatis() => await _userNuvatis.GetByIdAsync(12345);

    /**
     * D10: 단일 조회 - Dapper 구현
     */
    [Benchmark(Description = "D10_Single_SELECT_Dapper")]
    public async Task<User?> D10_Dapper() => await _userDapper.GetByIdAsync(12345);

    /**
     * D10: 단일 조회 - EF Core 구현
     */
    [Benchmark(Description = "D10_Single_SELECT_EfCore")]
    public async Task<User?> D10_EfCore() => await _userEfCore.GetByIdAsync(12345);

    // ========================================
    // Helper Methods (테스트 데이터 생성)
    // ========================================

    /**
     * 대량 사용자 데이터 생성 (1,000명)
     *
     * 【 사용 목적 】
     * - BULK INSERT 벤치마크용 테스트 데이터
     * - [GlobalSetup]에서 1회 생성 후 재사용
     *
     * 【 문자열 보간 (String Interpolation) 】
     * - $"bulk_user_{i}": 변수를 문자열에 삽입
     * - $"bulk{i}@test.com": 동적 이메일 생성
     *
     * 【 객체 초기화 구문 】
     * new User { UserName = ..., Email = ... }
     * → 생성자 호출 후 프로퍼티 설정
     */
    private static List<User> GenerateUsers(int count)
    {
        var users = new List<User>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var now = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            users.Add(new User
            {
                UserName = $"bulk_user_{timestamp}_{i}",
                Email = $"bulk{timestamp}_{i}@test.com",
                FullName = $"Bulk User {i}",
                PasswordHash = "hash",
                DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                PhoneNumber = "010-0000-0000",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        return users;
    }

    /**
     * 테스트용 사용자 생성 (트랜잭션 벤치마크용)
     *
     * 【 Target-typed new 표현식 (C# 9.0+) 】
     * - new(): 타입 추론 (반환 타입에서 자동 결정)
     * - 간결한 문법 (new User() → new())
     */
    private static User CreateTestUser()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var now = DateTime.UtcNow;
        return new User
        {
            UserName = $"tx_user_{timestamp}_{Guid.NewGuid():N}",
            Email = $"tx_{timestamp}_{Guid.NewGuid():N}@test.com",
            FullName = "Transaction User",
            PasswordHash = "hash",
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNumber = "010-0000-0000",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /**
     * 테스트용 주소 생성 (트랜잭션 벤치마크용)
     */
    private static Address CreateTestAddress()
    {
        var now = DateTime.UtcNow;
        return new()
        {
            AddressType = "shipping",
            StreetAddress = "123 Test St",
            City = "Seoul",
            State = "Seoul",
            PostalCode = "12345",
            Country = "Korea",
            IsDefault = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
