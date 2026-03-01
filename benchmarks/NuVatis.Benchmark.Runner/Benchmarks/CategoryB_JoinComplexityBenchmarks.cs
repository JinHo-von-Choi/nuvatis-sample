using BenchmarkDotNet.Attributes;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Runner.Benchmarks;

/**
 * 카테고리 B: JOIN 복잡도 벤치마크 - 2~3개 테이블 조인 성능 비교
 *
 * 【 벤치마크 목적 】
 * - ORM별 JOIN 쿼리 성능 비교 (NuVatis vs Dapper vs EF Core)
 * - 2-table JOIN vs 3-table JOIN 성능 차이 측정
 * - N번 반복 시 누적 성능 및 메모리 사용량 분석
 *
 * 【 시나리오 】
 * - B01-B02: 2-table JOIN (User + Addresses) - 1:N 관계
 * - B03-B05: 3-table JOIN (Order + User + OrderItems) - 복합 관계
 * - B06-B10: JOIN 10회 반복 - 누적 성능 측정
 * - B11-B13: 복합 쿼리 (사용자별 주문 목록) - WHERE + ORDER BY
 *
 * 【 JOIN이란? 】
 * - 여러 테이블의 데이터를 연결하여 하나의 결과 집합으로 반환
 * - Foreign Key 관계를 활용하여 관련 데이터 조회
 * - 예: User + Addresses → "홍길동" 사용자의 "집 주소", "회사 주소" 함께 조회
 *
 * 【 JOIN 종류 】
 * - INNER JOIN: 양쪽 테이블에 매칭되는 행만 반환
 * - LEFT JOIN: 왼쪽 테이블의 모든 행 반환 (오른쪽 NULL 가능)
 * - RIGHT JOIN: 오른쪽 테이블의 모든 행 반환 (왼쪽 NULL 가능)
 * - FULL JOIN: 양쪽 테이블의 모든 행 반환 (매칭 없어도 포함)
 *
 * 【 1:N 관계 JOIN 예시 】
 * SELECT u.*, a.*
 * FROM users u
 * LEFT JOIN addresses a ON u.id = a.user_id
 * WHERE u.id = 12345;
 *
 * 결과:
 * | user_id | user_name | address_id | address      |
 * |---------|-----------|------------|--------------|
 * | 12345   | 홍길동    | 1          | 서울 강남구  |
 * | 12345   | 홍길동    | 2          | 부산 해운대  |
 *
 * 【 성능 목표 】
 * - 2-table JOIN: 2-5ms
 * - 3-table JOIN: 5-15ms
 * - JOIN 10회 반복: 50-150ms
 *
 * 【 BenchmarkDotNet 설정 】
 * - [MemoryDiagnoser]: 메모리 할당량 측정
 * - [RankColumn]: ORM 간 순위 표시
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
[MemoryDiagnoser]
[RankColumn]
public class CategoryB_JoinComplexityBenchmarks
{
    /**
     * User Repository 인스턴스 (ORM별)
     *
     * 【 null! 연산자 】
     * - !: Null-forgiving operator (C# 8.0+)
     * - 컴파일러에게 "null이 아님을 보장"하겠다고 선언
     * - [GlobalSetup]에서 초기화되므로 실제로 null이 아님
     */
    private IUserRepository _userNuvatis = null!;
    private IUserRepository _userDapper = null!;
    private IUserRepository _userEfCore = null!;

    /**
     * Order Repository 인스턴스 (ORM별)
     */
    private IOrderRepository _orderNuvatis = null!;
    private IOrderRepository _orderDapper = null!;
    private IOrderRepository _orderEfCore = null!;

    /**
     * 벤치마크 시작 전 초기화 (1회 실행)
     *
     * 【 [GlobalSetup] 어트리뷰트 】
     * - BenchmarkDotNet이 모든 벤치마크 실행 전 1회만 호출
     * - DB 연결, DI 컨테이너 초기화 등 공통 설정
     * - 벤치마크 시간 측정에서 제외됨
     *
     * 【 DI (Dependency Injection) 패턴 】
     * - Repository 구현체를 외부에서 주입받음
     * - 테스트 시 Mock Repository로 교체 가능
     * - ORM별 구현체 분리로 일관된 인터페이스 사용
     *
     * 【 초기화 예시 】
     * var services = new ServiceCollection();
     * services.AddSingleton<IUserRepository, NuVatisUserRepository>();
     * services.AddSingleton<IUserRepository, DapperUserRepository>();
     * services.AddSingleton<IUserRepository, EfCoreUserRepository>();
     * var provider = services.BuildServiceProvider();
     *
     * _userNuvatis = provider.GetRequiredService<IUserRepository>();
     */
    [GlobalSetup]
    public void Setup()
    {
        // TODO: DI 컨테이너에서 주입
    }

    // ========================================
    // B01-B02: 2-table JOIN (User + Addresses)
    // ========================================

    /**
     * B01: 2-table JOIN 벤치마크 - NuVatis 구현
     *
     * 【 SQL 쿼리 】
     * SELECT u.*, a.*
     * FROM users u
     * LEFT JOIN addresses a ON u.id = a.user_id
     * WHERE u.id = @id;
     *
     * 【 1:N 관계 매핑 】
     * - User (1) ← Addresses (N)
     * - 한 사용자가 여러 주소를 가질 수 있음
     * - LEFT JOIN 사용 → 주소가 없어도 사용자 반환
     *
     * 【 NuVatis 특징 】
     * - XML 매퍼에서 <collection> 태그로 1:N 매핑
     * - ResultMap으로 복잡한 매핑 구성 가능
     * - MyBatis와 동일한 문법 (Java 개발자 친숙)
     *
     * 【 예상 성능 】
     * - 응답 시간: 2-5ms
     * - 메모리 할당: 1-2 KB
     *
     * 【 반환 타입 】
     * - Task<User?>: 비동기 작업 (await 사용)
     * - User?: Nullable 참조 타입 (사용자가 없을 수 있음)
     */
    [Benchmark(Description = "B01_2table_JOIN_NuVatis")]
    public async Task<User?> B01_NuVatis() => await _userNuvatis.GetWithAddressesAsync(12345);

    /**
     * B01: 2-table JOIN 벤치마크 - Dapper 구현
     *
     * 【 Dapper 특징 】
     * - Micro ORM: 경량, 빠른 성능
     * - SQL 문자열 직접 작성
     * - 1:N 매핑: SplitOn 파라미터 사용
     *
     * 【 Dapper 코드 예시 】
     * var sql = @"
     *     SELECT u.*, a.*
     *     FROM users u
     *     LEFT JOIN addresses a ON u.id = a.user_id
     *     WHERE u.id = @id
     * ";
     *
     * var userDict = new Dictionary<long, User>();
     * await connection.QueryAsync<User, Address, User>(
     *     sql,
     *     (user, address) =>
     *     {
     *         if (!userDict.TryGetValue(user.Id, out var currentUser))
     *         {
     *             currentUser = user;
     *             currentUser.Addresses = new List<Address>();
     *             userDict.Add(user.Id, currentUser);
     *         }
     *         if (address != null)
     *             currentUser.Addresses.Add(address);
     *         return currentUser;
     *     },
     *     new { id = 12345 },
     *     splitOn: "id"  // Address의 시작 컬럼
     * );
     *
     * 【 장점 】
     * - 빠른 성능 (거의 네이티브 ADO.NET 수준)
     * - 간단한 API
     * - 낮은 메모리 사용량
     *
     * 【 단점 】
     * - SQL 문자열 직접 작성 (오타 위험)
     * - 1:N 매핑 수동 구성 (복잡)
     * - 컴파일 타임 체크 부재
     */
    [Benchmark(Description = "B01_2table_JOIN_Dapper")]
    public async Task<User?> B01_Dapper() => await _userDapper.GetWithAddressesAsync(12345);

    /**
     * B01: 2-table JOIN 벤치마크 - EF Core 구현
     *
     * 【 EF Core 특징 】
     * - Full-featured ORM: 강력한 기능, 높은 추상화
     * - LINQ 쿼리: C# 문법으로 쿼리 작성
     * - Include() 메서드로 Eager Loading
     *
     * 【 EF Core 코드 예시 】
     * var user = await context.Users
     *     .Where(u => u.Id == 12345)
     *     .Include(u => u.Addresses)  // Eager Loading
     *     .FirstOrDefaultAsync();
     *
     * 【 LINQ 쿼리 번역 】
     * - EF Core가 LINQ를 SQL로 자동 변환
     * - Where() → WHERE 절
     * - Include() → LEFT JOIN
     * - FirstOrDefaultAsync() → LIMIT 1
     *
     * 【 장점 】
     * - 타입 안전성 (컴파일 타임 체크)
     * - Change Tracking (변경 추적)
     * - 마이그레이션 지원
     * - 강력한 LINQ API
     *
     * 【 단점 】
     * - 상대적으로 느린 성능 (추상화 오버헤드)
     * - 높은 메모리 사용량 (Change Tracking)
     * - 복잡한 쿼리 생성 시 비효율적인 SQL 가능
     */
    [Benchmark(Description = "B01_2table_JOIN_EfCore")]
    public async Task<User?> B01_EfCore() => await _userEfCore.GetWithAddressesAsync(12345);

    // ========================================
    // B03-B05: 3-table JOIN (Order + User + OrderItems)
    // ========================================

    /**
     * B03: 3-table JOIN 벤치마크 - NuVatis 구현
     *
     * 【 SQL 쿼리 】
     * SELECT o.*, u.user_name, u.email,
     *        oi.id AS order_item_id, oi.product_id, oi.quantity, oi.price
     * FROM orders o
     * JOIN users u ON o.user_id = u.id
     * LEFT JOIN order_items oi ON o.id = oi.order_id
     * WHERE o.id = @id;
     *
     * 【 복합 관계 매핑 】
     * - Order (1) → User (1): N:1 관계 (주문자 정보)
     * - Order (1) ← OrderItems (N): 1:N 관계 (주문 항목)
     *
     * 【 JOIN 순서 중요성 】
     * - INNER JOIN users: 주문에 반드시 사용자 존재 (필수)
     * - LEFT JOIN order_items: 주문 항목이 없을 수도 있음 (선택)
     *
     * 【 2-table vs 3-table JOIN 성능 차이 】
     * - 2-table: 2-5ms (User + Addresses)
     * - 3-table: 5-15ms (Order + User + OrderItems)
     * - 테이블 수 증가 → 조인 비용 증가
     *
     * 【 NuVatis ResultMap 예시 】
     * <resultMap id="OrderWithUserAndItemsMap" type="Order">
     *   <id property="Id" column="id"/>
     *   <result property="TotalAmount" column="total_amount"/>
     *
     *   <association property="User" javaType="User">
     *     <id property="Id" column="user_id"/>
     *     <result property="UserName" column="user_name"/>
     *   </association>
     *
     *   <collection property="OrderItems" ofType="OrderItem">
     *     <id property="Id" column="order_item_id"/>
     *     <result property="ProductId" column="product_id"/>
     *     <result property="Quantity" column="quantity"/>
     *   </collection>
     * </resultMap>
     *
     * 【 예상 성능 】
     * - 응답 시간: 5-15ms
     * - 메모리 할당: 3-5 KB
     */
    [Benchmark(Description = "B03_3table_JOIN_NuVatis")]
    public async Task<Order?> B03_NuVatis() => await _orderNuvatis.GetWithUserAndItemsAsync(100001);

    /**
     * B03: 3-table JOIN 벤치마크 - Dapper 구현
     *
     * 【 Dapper Multi-Mapping 】
     * - 3개 타입을 동시에 매핑
     * - splitOn으로 각 타입의 시작 컬럼 지정
     *
     * 【 Dapper 코드 예시 】
     * var sql = @"
     *     SELECT o.*, u.*, oi.*
     *     FROM orders o
     *     JOIN users u ON o.user_id = u.id
     *     LEFT JOIN order_items oi ON o.id = oi.order_id
     *     WHERE o.id = @id
     * ";
     *
     * var orderDict = new Dictionary<long, Order>();
     * await connection.QueryAsync<Order, User, OrderItem, Order>(
     *     sql,
     *     (order, user, orderItem) =>
     *     {
     *         if (!orderDict.TryGetValue(order.Id, out var currentOrder))
     *         {
     *             currentOrder = order;
     *             currentOrder.User = user;
     *             currentOrder.OrderItems = new List<OrderItem>();
     *             orderDict.Add(order.Id, currentOrder);
     *         }
     *         if (orderItem != null)
     *             currentOrder.OrderItems.Add(orderItem);
     *         return currentOrder;
     *     },
     *     new { id = 100001 },
     *     splitOn: "id,id"  // User의 id, OrderItem의 id
     * );
     *
     * 【 splitOn 파라미터 】
     * - "id,id": 첫 번째 id는 User 시작, 두 번째 id는 OrderItem 시작
     * - Dapper가 컬럼을 각 타입으로 분할하는 기준
     *
     * 【 Dictionary 패턴 】
     * - 주문이 중복으로 반환되므로 Dictionary로 중복 제거
     * - Key: Order.Id
     * - Value: Order 객체 (User, OrderItems 포함)
     */
    [Benchmark(Description = "B03_3table_JOIN_Dapper")]
    public async Task<Order?> B03_Dapper() => await _orderDapper.GetWithUserAndItemsAsync(100001);

    /**
     * B03: 3-table JOIN 벤치마크 - EF Core 구현
     *
     * 【 EF Core Include() 체이닝 】
     * - Include()를 여러 번 호출하여 다중 관계 로드
     * - ThenInclude()로 중첩 관계 로드 가능
     *
     * 【 EF Core 코드 예시 】
     * var order = await context.Orders
     *     .Where(o => o.Id == 100001)
     *     .Include(o => o.User)        // Order → User
     *     .Include(o => o.OrderItems)  // Order → OrderItems
     *     .FirstOrDefaultAsync();
     *
     * 【 생성되는 SQL 】
     * EF Core가 자동으로 LEFT JOIN 생성:
     * SELECT o.*, u.*, oi.*
     * FROM orders o
     * LEFT JOIN users u ON o.user_id = u.id
     * LEFT JOIN order_items oi ON o.id = oi.order_id
     * WHERE o.id = 100001
     *
     * 【 Change Tracking 오버헤드 】
     * - EF Core는 조회한 엔티티를 추적 (변경 감지)
     * - 읽기 전용 쿼리: .AsNoTracking() 사용 가능
     *
     * 【 AsNoTracking() 최적화 】
     * var order = await context.Orders
     *     .AsNoTracking()  // 추적 비활성화
     *     .Where(o => o.Id == 100001)
     *     .Include(o => o.User)
     *     .Include(o => o.OrderItems)
     *     .FirstOrDefaultAsync();
     *
     * 성능 개선: 10-30% 빠름 (메모리 사용량도 감소)
     */
    [Benchmark(Description = "B03_3table_JOIN_EfCore")]
    public async Task<Order?> B03_EfCore() => await _orderEfCore.GetWithUserAndItemsAsync(100001);

    // ========================================
    // B06-B10: JOIN 10회 반복 - 누적 성능 측정
    // ========================================

    /**
     * B06: JOIN 10회 반복 벤치마크 - NuVatis 구현
     *
     * 【 반복 쿼리 시나리오 】
     * - 연속된 주문 ID (100001 ~ 100010) 조회
     * - 각 조회마다 3-table JOIN 실행
     * - 총 10번의 DB 왕복
     *
     * 【 루프 vs 일괄 조회 비교 】
     * [비권장] 루프 (현재 방식):
     *   for (int i = 0; i < 10; i++)
     *   {
     *       await GetOrderAsync(100001 + i);  // 10번 쿼리
     *   }
     *   → 네트워크 왕복: 10회
     *   → 응답 시간: 50-150ms
     *
     * [권장] 일괄 조회 (IN 절):
     *   SELECT * FROM orders
     *   WHERE id IN (100001, 100002, ..., 100010);
     *   → 네트워크 왕복: 1회
     *   → 응답 시간: 10-30ms
     *
     * 성능 개선: 3-5배 빠름
     *
     * 【 벤치마크 목적 】
     * - 반복 호출 시 ORM별 누적 성능 비교
     * - 메모리 할당 패턴 분석 (GC 압력)
     * - 연결 풀(Connection Pool) 효율성 측정
     *
     * 【 연결 풀(Connection Pool) 개념 】
     * - DB 연결을 재사용하여 성능 향상
     * - 매번 새 연결 생성 비용 절감
     * - 풀 크기: 기본 100개 (설정 가능)
     *
     * 【 예상 성능 】
     * - 응답 시간: 50-150ms (10번 × 5-15ms)
     * - 메모리 할당: 30-50 KB
     * - GC Gen0: 5-10회
     *
     * 【 async/await 루프 패턴 】
     * - await를 루프 내부에서 호출 → 순차 실행
     * - 각 조회가 완료되어야 다음 조회 시작
     * - 병렬 실행이 필요하면 Task.WhenAll() 사용
     *
     * 【 병렬 실행 예시 】
     * var tasks = new List<Task<Order>>();
     * for (int i = 0; i < 10; i++)
     * {
     *     int id = 100001 + i;
     *     tasks.Add(GetOrderAsync(id));
     * }
     * Order[] orders = await Task.WhenAll(tasks);
     *
     * → 10개 쿼리를 동시에 실행 (병렬)
     * → 응답 시간: 15-30ms (가장 느린 쿼리 기준)
     */
    [Benchmark(Description = "B06_JOIN_10x_NuVatis")]
    public async Task B06_NuVatis()
    {
        for (int i = 0; i < 10; i++)
        {
            await _orderNuvatis.GetWithUserAndItemsAsync(100001 + i);
        }
    }

    /**
     * B06: JOIN 10회 반복 벤치마크 - Dapper 구현
     *
     * 【 Dapper 특성 】
     * - 경량 ORM → 낮은 메모리 할당
     * - 직접 ADO.NET 사용 → 빠른 성능
     * - 연결 풀 효율적 사용
     *
     * 【 예상 성능 비교 】
     * - NuVatis: 60-120ms (XML 파싱 오버헤드)
     * - Dapper: 50-100ms (가장 빠름)
     * - EF Core: 80-150ms (Change Tracking 오버헤드)
     *
     * 【 Dapper 최적화 팁 】
     * - SQL 쿼리 캐싱: 동일 쿼리 재사용
     * - buffered: false 옵션 (스트리밍)
     * - CommandDefinition으로 타임아웃 설정
     */
    [Benchmark(Description = "B06_JOIN_10x_Dapper")]
    public async Task B06_Dapper()
    {
        for (int i = 0; i < 10; i++)
        {
            await _orderDapper.GetWithUserAndItemsAsync(100001 + i);
        }
    }

    /**
     * B06: JOIN 10회 반복 벤치마크 - EF Core 구현
     *
     * 【 EF Core Change Tracking 누적 】
     * - 각 조회마다 엔티티를 추적 (메모리 증가)
     * - 10번 조회 시 10개 Order + 10개 User + N개 OrderItems 추적
     * - 추적 비용: 엔티티당 수백 바이트
     *
     * 【 AsNoTracking() 최적화 】
     * - 읽기 전용 쿼리에서 추적 비활성화
     * - 메모리 사용량 30-50% 감소
     * - 응답 시간 10-20% 개선
     *
     * 【 EF Core 컨텍스트 수명 】
     * - DbContext는 단기 수명 (요청당 1개)
     * - 장기 사용 시 메모리 누수 가능
     * - 벤치마크에서는 [GlobalSetup]에서 생성
     *
     * 【 예상 메모리 할당 】
     * - NuVatis: 30-40 KB
     * - Dapper: 25-35 KB (가장 낮음)
     * - EF Core: 40-60 KB (Change Tracking 비용)
     */
    [Benchmark(Description = "B06_JOIN_10x_EfCore")]
    public async Task B06_EfCore()
    {
        for (int i = 0; i < 10; i++)
        {
            await _orderEfCore.GetWithUserAndItemsAsync(100001 + i);
        }
    }

    // ========================================
    // B11-B13: 복합 쿼리 (WHERE + ORDER BY)
    // ========================================

    /**
     * B11: 사용자별 주문 목록 조회 - NuVatis 구현
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM orders
     * WHERE user_id = @userId
     * ORDER BY created_at DESC;
     *
     * 【 WHERE + ORDER BY 조합 】
     * - WHERE: 특정 사용자의 주문만 필터링
     * - ORDER BY: 최신 주문부터 정렬 (DESC)
     * - 인덱스: idx_orders_user_created (user_id, created_at DESC)
     *
     * 【 복합 인덱스 활용 】
     * CREATE INDEX idx_orders_user_created
     * ON orders(user_id, created_at DESC);
     *
     * → WHERE와 ORDER BY를 모두 인덱스로 처리 (빠름)
     *
     * [권장] 복합 인덱스 사용:
     *   1. user_id로 필터링 (인덱스 스캔)
     *   2. created_at으로 정렬 (인덱스 순서 사용)
     *   → 응답 시간: 5-20ms
     *
     * [비권장] 단일 인덱스만 사용:
     *   CREATE INDEX idx_orders_user ON orders(user_id);
     *   → user_id로 필터링 후 메모리에서 정렬
     *   → 응답 시간: 10-50ms (정렬 비용 추가)
     *
     * 【 사용자별 평균 주문 건수 】
     * - 평균: 0-300건 (3년치)
     * - 활성 사용자: 10-50건
     * - 비활성 사용자: 0건
     *
     * 【 반환 타입 】
     * - Task<IEnumerable<Order>>: 비동기 컬렉션
     * - IEnumerable<T>: 지연 실행 (Lazy Evaluation)
     *
     * 【 IEnumerable vs List 차이 】
     * IEnumerable<Order>:
     *   - 인터페이스 (추상)
     *   - 지연 실행 (필요할 때 로드)
     *   - 메모리 효율적
     *   - 읽기 전용
     *
     * List<Order>:
     *   - 구체 타입
     *   - 즉시 실행 (전체 로드)
     *   - 메모리 사용량 높음
     *   - 추가/삭제 가능
     *
     * 【 예상 성능 】
     * - 응답 시간: 5-20ms (사용자당 평균 10-50건)
     * - 메모리 할당: 5-10 KB
     *
     * 【 NuVatis XML 매퍼 예시 】
     * <select id="GetByUserIdAsync" resultType="Order">
     *   SELECT * FROM orders
     *   WHERE user_id = #{userId}
     *   ORDER BY created_at DESC
     * </select>
     */
    [Benchmark(Description = "B11_Multiple_Query_NuVatis")]
    public async Task<IEnumerable<Order>> B11_NuVatis() =>
        await _orderNuvatis.GetByUserIdAsync(12345);

    /**
     * B11: 사용자별 주문 목록 조회 - Dapper 구현
     *
     * 【 Dapper 코드 예시 】
     * var sql = @"
     *     SELECT * FROM orders
     *     WHERE user_id = @userId
     *     ORDER BY created_at DESC
     * ";
     *
     * var orders = await connection.QueryAsync<Order>(
     *     sql,
     *     new { userId = 12345 }
     * );
     *
     * return orders;  // IEnumerable<Order>
     *
     * 【 Dapper QueryAsync() 】
     * - 비동기로 여러 행 조회
     * - 자동으로 Order 객체에 매핑
     * - IEnumerable<T> 반환
     *
     * 【 Dapper 매핑 규칙 】
     * - 컬럼명 → 프로퍼티명 자동 매핑
     * - user_id → UserId (snake_case → PascalCase)
     * - created_at → CreatedAt
     * - 대소문자 무시 (case-insensitive)
     *
     * 【 Dapper 장점 (이 시나리오) 】
     * - 간단한 SQL → 직관적 코드
     * - 빠른 성능 (거의 네이티브)
     * - 낮은 메모리 사용량
     */
    [Benchmark(Description = "B11_Multiple_Query_Dapper")]
    public async Task<IEnumerable<Order>> B11_Dapper() =>
        await _orderDapper.GetByUserIdAsync(12345);

    /**
     * B11: 사용자별 주문 목록 조회 - EF Core 구현
     *
     * 【 EF Core LINQ 쿼리 】
     * var orders = await context.Orders
     *     .Where(o => o.UserId == 12345)
     *     .OrderByDescending(o => o.CreatedAt)
     *     .ToListAsync();
     *
     * return orders;  // List<Order>
     *
     * 【 LINQ 메서드 체이닝 】
     * - Where(): 조건 필터링 (SQL WHERE)
     * - OrderByDescending(): 내림차순 정렬 (SQL ORDER BY DESC)
     * - ToListAsync(): 비동기 실행 및 List 변환
     *
     * 【 EF Core 쿼리 번역 】
     * LINQ → SQL 자동 변환:
     *   Where(o => o.UserId == 12345)
     *   → WHERE user_id = 12345
     *
     *   OrderByDescending(o => o.CreatedAt)
     *   → ORDER BY created_at DESC
     *
     *   ToListAsync()
     *   → 쿼리 실행 및 전체 로드
     *
     * 【 ToListAsync() vs ToArrayAsync() vs AsEnumerable() 】
     * ToListAsync():
     *   - List<T> 반환 (추가/삭제 가능)
     *   - 메모리에 전체 로드
     *   - Change Tracking 활성화
     *
     * ToArrayAsync():
     *   - T[] 반환 (읽기 전용)
     *   - 메모리에 전체 로드
     *
     * AsEnumerable():
     *   - IEnumerable<T> 반환
     *   - 지연 실행 (서버 측)
     *   - LINQ to Objects로 전환
     *
     * 【 EF Core 최적화 팁 】
     * - 읽기 전용: .AsNoTracking()
     * - 필요한 컬럼만: .Select(o => new { o.Id, o.TotalAmount })
     * - 페이징: .Skip(offset).Take(limit)
     *
     * 【 AsNoTracking() 예시 】
     * var orders = await context.Orders
     *     .AsNoTracking()  // Change Tracking 비활성화
     *     .Where(o => o.UserId == 12345)
     *     .OrderByDescending(o => o.CreatedAt)
     *     .ToListAsync();
     *
     * → 메모리 사용량 20-30% 감소
     * → 응답 시간 10-15% 개선
     */
    [Benchmark(Description = "B11_Multiple_Query_EfCore")]
    public async Task<IEnumerable<Order>> B11_EfCore() =>
        await _orderEfCore.GetByUserIdAsync(12345);
}
