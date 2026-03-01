using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Core.Interfaces;

/**
 * 사용자 Repository 인터페이스 - 데이터 접근 계층(Data Access Layer) 추상화
 *
 * 【 Repository 패턴이란? 】
 * - 데이터베이스 접근 로직을 캡슐화하는 디자인 패턴
 * - 비즈니스 로직과 데이터 접근 로직 분리 (관심사의 분리)
 * - 장점:
 *   1. 테스트 용이성: Mock Repository로 단위 테스트 가능
 *   2. 유지보수성: SQL 변경 시 Repository만 수정
 *   3. ORM 교체 용이: Dapper → EF Core 전환 시 인터페이스는 그대로
 *
 * 【 인터페이스(Interface)란? 】
 * - 구현 없이 메서드 시그니처만 정의하는 계약(Contract)
 * - 여러 구현체를 동일한 방식으로 사용 (다형성)
 * - 예시:
 *   IUserRepository repo = new DapperUserRepository();  // Dapper 사용
 *   IUserRepository repo = new EfCoreUserRepository();  // EF Core 사용
 *   // 동일한 인터페이스로 다른 구현체 사용 가능
 *
 * 【 벤치마크 시나리오 】
 * - Simple: 단순 CRUD (GetById, Insert, Update 등)
 * - Medium: JOIN, GROUP BY, Transaction 등
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public interface IUserRepository
{
    // ========================================
    // Simple 시나리오: 기본 CRUD 작업
    // ========================================

    /**
     * 사용자 ID로 단건 조회 (Primary Key 조회)
     *
     * 【 SQL 쿼리 】
     * SELECT id, user_name, email, full_name, password_hash,
     *        date_of_birth, phone_number, is_active, created_at, updated_at
     * FROM users
     * WHERE id = @id;
     *
     * 【 .NET 비동기(async) 개념 】
     * - Task<T>: 비동기 작업을 나타내는 객체
     * - async/await: 비동기 프로그래밍 패턴
     *
     * 【 왜 비동기? 】
     * - DB 쿼리는 I/O 작업 (네트워크 통신)
     * - 동기 방식: 쿼리 결과가 올 때까지 스레드 대기 (비효율)
     *   User user = GetById(1); // 10ms 동안 스레드 블로킹
     *
     * - 비동기 방식: 쿼리 중에 스레드는 다른 작업 수행
     *   Task<User?> task = GetByIdAsync(1); // 즉시 반환
     *   // 스레드는 다른 요청 처리 가능
     *   User? user = await task; // 결과 준비되면 재개
     *
     * 【 Task<User?> 설명 】
     * - Task<T>: 미래에 완료될 작업, 완료 시 T 타입 반환
     * - User?: Nullable Reference Type
     *   - User 객체 반환 (조회 성공)
     *   - null 반환 (해당 ID 없음)
     *
     * 【 사용 예시 】
     * var user = await userRepository.GetByIdAsync(12345);
     * if (user == null)
     * {
     *     Console.WriteLine("사용자 없음");
     * }
     * else
     * {
     *     Console.WriteLine($"이름: {user.FullName}");
     * }
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 예상 응답 시간: <1ms (인덱스 조회)
     */
    Task<User?> GetByIdAsync(long id);

    /**
     * 이메일 도메인으로 사용자 목록 조회 (WHERE 조건 조회)
     *
     * 【 SQL 쿼리 】
     * SELECT *
     * FROM users
     * WHERE email LIKE '%@' || @domain;
     *
     * 예: domain = "gmail.com"
     *     → WHERE email LIKE '%@gmail.com'
     *
     * 【 LIKE 연산자 】
     * - %: 0개 이상의 임의 문자
     *   'a%': a로 시작
     *   '%a': a로 끝남
     *   '%a%': a 포함
     *
     * 【 주의사항 】
     * - LIKE '%...': Full Table Scan (인덱스 미사용)
     * - 대량 데이터에서는 느림 (100K 건 → 100ms 이상)
     *
     * 【 .NET 개념 】
     * - IEnumerable<User>: 순회 가능한 컬렉션 (지연 실행)
     * - Task<IEnumerable<User>>: 비동기로 컬렉션 반환
     *
     * 【 IEnumerable vs List 】
     * - IEnumerable: 인터페이스, 순회만 가능 (읽기 전용)
     * - List: 구현 클래스, 추가/삭제 가능
     * - 반환 타입은 IEnumerable (호출자가 필요 시 .ToList())
     *
     * 【 사용 예시 】
     * var gmailUsers = await userRepository.GetByEmailDomainAsync("gmail.com");
     * foreach (var user in gmailUsers)
     * {
     *     Console.WriteLine(user.Email);
     * }
     *
     * // LINQ로 카운트
     * int count = gmailUsers.Count();
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - Full Table Scan
     * - 예상 응답 시간: 1-10ms (결과 건수에 비례)
     */
    Task<IEnumerable<User>> GetByEmailDomainAsync(string domain);

    /**
     * 페이징 조회 (OFFSET/LIMIT)
     *
     * 【 SQL 쿼리 】
     * SELECT *
     * FROM users
     * ORDER BY id
     * LIMIT @limit OFFSET @offset;
     *
     * 【 페이징 개념 】
     * - OFFSET: 건너뛸 레코드 수
     * - LIMIT: 가져올 레코드 수
     *
     * 예: offset=20, limit=10
     *     → 21번째~30번째 레코드 조회 (3페이지, 페이지당 10건)
     *
     * 【 페이지 번호 → OFFSET 계산 】
     * int pageNumber = 3;  // 3페이지
     * int pageSize = 10;   // 페이지당 10건
     * int offset = (pageNumber - 1) * pageSize; // (3-1) * 10 = 20
     * int limit = pageSize; // 10
     *
     * 【 ORDER BY 필수 】
     * - OFFSET/LIMIT만 사용 시 순서 보장 안 됨
     * - ORDER BY id: Primary Key 순서 (일관성 보장)
     *
     * 【 OFFSET의 문제점 】
     * - OFFSET 10000: 1만 건을 읽고 버림 (비효율)
     * - 대안: Keyset Pagination (마지막 ID 기준)
     *   WHERE id > @lastId ORDER BY id LIMIT 10
     *
     * 【 사용 예시 】
     * int page = 1;
     * int size = 20;
     * var users = await userRepository.GetPagedAsync((page - 1) * size, size);
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(offset + limit)
     * - OFFSET 작을 때: <5ms
     * - OFFSET 클 때 (10K+): 50-200ms
     */
    Task<IEnumerable<User>> GetPagedAsync(int offset, int limit);

    /**
     * 사용자 삽입 (INSERT)
     *
     * 【 SQL 쿼리 】
     * INSERT INTO users (user_name, email, full_name, password_hash,
     *                     date_of_birth, phone_number, is_active, created_at, updated_at)
     * VALUES (@userName, @email, @fullName, @passwordHash,
     *         @dateOfBirth, @phoneNumber, @isActive, NOW(), NOW())
     * RETURNING id;
     *
     * 【 RETURNING 절 】
     * - PostgreSQL 전용 (MySQL은 LAST_INSERT_ID())
     * - 삽입된 레코드의 id 반환
     *
     * 【 AUTO_INCREMENT 동작 】
     * 1. INSERT 실행
     * 2. DB가 자동으로 id 할당 (예: 100001)
     * 3. RETURNING id → 100001 반환
     *
     * 【 .NET 개념 】
     * - Task<long>: 비동기로 long 타입 반환
     * - 반환값: 생성된 사용자의 ID
     *
     * 【 사용 예시 】
     * var newUser = new User
     * {
     *     UserName = "hong123",
     *     Email = "hong@example.com",
     *     FullName = "홍길동",
     *     PasswordHash = BCrypt.HashPassword("password"),
     *     CreatedAt = DateTime.UtcNow,
     *     UpdatedAt = DateTime.UtcNow
     * };
     *
     * long userId = await userRepository.InsertAsync(newUser);
     * Console.WriteLine($"생성된 사용자 ID: {userId}");
     *
     * 【 주의사항 】
     * - Id는 INSERT 전에는 0 (또는 기본값)
     * - INSERT 후 RETURNING으로 받은 ID를 반환
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1)
     * - 예상 응답 시간: 2-5ms
     */
    Task<long> InsertAsync(User user);

    /**
     * 사용자 정보 수정 (UPDATE)
     *
     * 【 SQL 쿼리 】
     * UPDATE users
     * SET user_name = @userName,
     *     email = @email,
     *     full_name = @fullName,
     *     phone_number = @phoneNumber,
     *     is_active = @isActive,
     *     updated_at = NOW()
     * WHERE id = @id;
     *
     * 【 .NET 개념 】
     * - Task<int>: 비동기로 int 반환
     * - 반환값: 영향받은 행(Row) 수
     *   - 1: 수정 성공
     *   - 0: 해당 ID 없음 (수정 실패)
     *
     * 【 사용 예시 】
     * var user = await userRepository.GetByIdAsync(12345);
     * if (user != null)
     * {
     *     user.FullName = "김철수";
     *     user.UpdatedAt = DateTime.UtcNow;
     *
     *     int affectedRows = await userRepository.UpdateAsync(user);
     *     if (affectedRows > 0)
     *     {
     *         Console.WriteLine("수정 성공");
     *     }
     * }
     *
     * 【 주의사항 】
     * - WHERE 절 필수 (없으면 전체 레코드 수정됨!)
     *   UPDATE users SET full_name = '김철수';  // [위험] 전체 수정
     *   UPDATE users SET full_name = '김철수' WHERE id = 1; // [안전] 단건 수정
     *
     * - updated_at 자동 갱신 (DB 트리거 또는 애플리케이션)
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 예상 응답 시간: 2-5ms
     */
    Task<int> UpdateAsync(User user);

    // ========================================
    // Medium 시나리오: JOIN, 집계, 트랜잭션
    // ========================================

    /**
     * 사용자 + 주소 목록 조회 (2-Table JOIN)
     *
     * 【 SQL 쿼리 】
     * SELECT u.*, a.*
     * FROM users u
     * LEFT JOIN addresses a ON u.id = a.user_id
     * WHERE u.id = @id;
     *
     * 【 JOIN 종류 】
     * - INNER JOIN: 양쪽 테이블에 모두 존재하는 레코드만
     *   users 교집합 addresses (주소 없는 사용자 제외)
     *
     * - LEFT JOIN: 왼쪽(users) 테이블의 모든 레코드 + 매칭되는 오른쪽
     *   모든 사용자 (주소 없어도 포함)
     *
     * 【 ResultMap 처리 】
     * - 1개 User 객체에 여러 Address 객체 매핑
     * - user.Addresses = [addr1, addr2, addr3]
     *
     * 【 N+1 문제 회피 】
     * - [비효율] N+1 문제:
     *   1. SELECT * FROM users WHERE id = 1;      // 1번
     *   2. SELECT * FROM addresses WHERE user_id = 1; // N번 (사용자마다)
     *
     * - [효율] JOIN 사용:
     *   SELECT u.*, a.* FROM users u LEFT JOIN addresses a ON u.id = a.user_id WHERE u.id = 1;
     *   // 1번의 쿼리로 모두 조회
     *
     * 【 사용 예시 】
     * var user = await userRepository.GetWithAddressesAsync(12345);
     * if (user != null)
     * {
     *     Console.WriteLine($"사용자: {user.FullName}");
     *     foreach (var addr in user.Addresses ?? Enumerable.Empty<Address>())
     *     {
     *         Console.WriteLine($"주소: {addr.Street}, {addr.City}");
     *     }
     * }
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Indexed Nested Loop Join
     * - 예상 응답 시간: 5-10ms
     */
    Task<User?> GetWithAddressesAsync(long id);

    /**
     * 국가별 사용자 수 집계 (GROUP BY + COUNT)
     *
     * 【 SQL 쿼리 】
     * SELECT a.country, COUNT(DISTINCT u.id) AS user_count
     * FROM users u
     * JOIN addresses a ON u.id = a.user_id
     * GROUP BY a.country
     * ORDER BY user_count DESC;
     *
     * 【 GROUP BY 개념 】
     * - 특정 컬럼 기준으로 그룹화
     * - 집계 함수 (COUNT, SUM, AVG, MAX, MIN)와 함께 사용
     *
     * 예시 데이터:
     * | user_id | country |
     * |---------|---------|
     * | 1       | Korea   |
     * | 2       | Korea   |
     * | 3       | USA     |
     *
     * GROUP BY country → 결과:
     * | country | user_count |
     * |---------|------------|
     * | Korea   | 2          |
     * | USA     | 1          |
     *
     * 【 COUNT(DISTINCT u.id) 】
     * - DISTINCT: 중복 제거
     * - 한 사용자가 여러 주소를 가질 수 있으므로 중복 카운트 방지
     *
     * 【 .NET 개념 】
     * - Dictionary<string, int>: 키-값 쌍 컬렉션
     *   Key: country (예: "Korea")
     *   Value: user_count (예: 12345)
     *
     * 【 사용 예시 】
     * var countByCountry = await userRepository.GetUserCountByCountryAsync();
     * foreach (var kvp in countByCountry)
     * {
     *     Console.WriteLine($"{kvp.Key}: {kvp.Value}명");
     * }
     * // 출력: Korea: 50000명, USA: 30000명, Japan: 20000명
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - Full Table Scan + Aggregation
     * - 예상 응답 시간: 10-50ms (데이터량에 비례)
     */
    Task<Dictionary<string, int>> GetUserCountByCountryAsync();

    /**
     * 동적 검색 (Dynamic WHERE 조건)
     *
     * 【 SQL 쿼리 (동적 생성) 】
     * SELECT *
     * FROM users
     * WHERE 1=1
     *   AND (@userName IS NULL OR user_name LIKE '%' || @userName || '%')
     *   AND (@email IS NULL OR email LIKE '%' || @email || '%')
     *   AND (@isActive IS NULL OR is_active = @isActive);
     *
     * 【 동적 SQL 개념 】
     * - 파라미터에 따라 WHERE 조건이 달라짐
     * - null이면 해당 조건 무시 (모든 값 허용)
     *
     * 예시:
     * 1. SearchAsync("hong", null, null)
     *    → WHERE user_name LIKE '%hong%'
     *
     * 2. SearchAsync(null, "gmail", true)
     *    → WHERE email LIKE '%gmail%' AND is_active = TRUE
     *
     * 3. SearchAsync(null, null, null)
     *    → WHERE 1=1 (모든 사용자 조회)
     *
     * 【 WHERE 1=1 트릭 】
     * - 항상 참인 조건
     * - 동적으로 AND 조건 추가 시 편리
     *   WHERE 1=1 AND name LIKE ... AND email LIKE ...
     *
     * 【 .NET Nullable 파라미터 】
     * - string?: null 허용 (검색하지 않음)
     * - bool?: null 허용 (활성/비활성 모두)
     *
     * 【 사용 예시 】
     * // 사용자명에 "kim" 포함
     * var users = await userRepository.SearchAsync("kim", null, null);
     *
     * // Gmail 사용자 중 활성 상태만
     * var gmailActive = await userRepository.SearchAsync(null, "gmail", true);
     *
     * // 모든 조건
     * var result = await userRepository.SearchAsync("park", "naver", true);
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - LIKE 사용 시 Full Table Scan
     * - 예상 응답 시간: 5-50ms (결과 건수에 비례)
     */
    Task<IEnumerable<User>> SearchAsync(string? userName, string? email, bool? isActive);

    /**
     * 대량 삽입 (Bulk INSERT)
     *
     * 【 SQL 쿼리 (PostgreSQL COPY) 】
     * COPY users (user_name, email, full_name, password_hash, created_at, updated_at)
     * FROM STDIN BINARY;
     *
     * 【 Bulk INSERT vs 반복 INSERT 】
     * - [비효율] 반복 INSERT (1000번):
     *   foreach (var user in users) {
     *       INSERT INTO users VALUES (...);  // 1000번 실행
     *   }
     *   → 1000번의 네트워크 왕복 (RTT) → 느림 (1-5초)
     *
     * - [효율] Bulk INSERT (1번):
     *   COPY users FROM STDIN ...;  // 1번에 1000건 전송
     *   → 1번의 네트워크 왕복 → 빠름 (50-200ms)
     *
     * 【 PostgreSQL COPY 프로토콜 】
     * - 바이너리 스트림으로 대량 데이터 전송
     * - 일반 INSERT보다 10-100배 빠름
     *
     * 【 .NET 개념 】
     * - IEnumerable<User>: 컬렉션 인터페이스
     * - Task<int>: 삽입된 레코드 수 반환
     *
     * 【 사용 예시 】
     * var users = new List<User>();
     * for (int i = 0; i < 1000; i++)
     * {
     *     users.Add(new User
     *     {
     *         UserName = $"user{i}",
     *         Email = $"user{i}@example.com",
     *         FullName = $"사용자{i}",
     *         // ...
     *     });
     * }
     *
     * int insertedCount = await userRepository.BulkInsertAsync(users);
     * Console.WriteLine($"{insertedCount}건 삽입 완료");
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) (n = 삽입 건수)
     * - 1K 건: 50-200ms
     * - 10K 건: 500ms-2s
     * - 100K 건: 5-20s
     */
    Task<int> BulkInsertAsync(IEnumerable<User> users);

    /**
     * 사용자 + 주소 동시 삽입 (Transaction)
     *
     * 【 SQL 쿼리 】
     * BEGIN;
     *   INSERT INTO users (...) VALUES (...) RETURNING id;
     *   INSERT INTO addresses (user_id, ...) VALUES (@userId, ...);
     * COMMIT;
     *
     * 【 트랜잭션(Transaction) 개념 】
     * - 여러 SQL을 하나의 원자적(Atomic) 단위로 실행
     * - ACID 원칙:
     *   A (Atomicity): 모두 성공 또는 모두 실패 (부분 성공 없음)
     *   C (Consistency): 일관성 유지 (FK 제약 조건 등)
     *   I (Isolation): 다른 트랜잭션과 격리
     *   D (Durability): 커밋 후 영구 저장
     *
     * 【 왜 트랜잭션? 】
     * 시나리오: 사용자 생성 + 주소 추가
     *
     * [비권장] 트랜잭션 없이:
     *   1. INSERT INTO users ... → 성공 (user_id = 100)
     *   2. INSERT INTO addresses (user_id=100) ... → 실패 (네트워크 끊김)
     *   → 결과: 주소 없는 사용자만 생성됨 (데이터 불일치)
     *
     * [권장] 트랜잭션 사용:
     *   BEGIN;
     *     INSERT INTO users ...
     *     INSERT INTO addresses ...
     *   COMMIT; (모두 성공 시)
     *   또는 ROLLBACK; (하나라도 실패 시)
     *   → 결과: 모두 성공 또는 모두 취소 (데이터 일관성 보장)
     *
     * 【 .NET 트랜잭션 사용 】
     * using var transaction = connection.BeginTransaction();
     * try
     * {
     *     long userId = await InsertUserAsync(user, transaction);
     *     address.UserId = userId;
     *     await InsertAddressAsync(address, transaction);
     *
     *     transaction.Commit();
     * }
     * catch
     * {
     *     transaction.Rollback();
     *     throw;
     * }
     *
     * 【 사용 예시 】
     * var user = new User { ... };
     * var address = new Address
     * {
     *     Street = "강남대로 123",
     *     City = "서울",
     *     Country = "Korea"
     * };
     *
     * int result = await userRepository.InsertUserWithAddressAsync(user, address);
     * // result > 0: 성공 (사용자 + 주소 모두 생성)
     * // result = 0: 실패 (아무것도 생성 안 됨)
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1)
     * - 예상 응답 시간: 5-15ms (2번의 INSERT + 트랜잭션 오버헤드)
     */
    Task<int> InsertUserWithAddressAsync(User user, Address address);
}
