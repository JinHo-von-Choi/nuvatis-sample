using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Core.Interfaces;

/**
 * 주문 Repository 인터페이스 - 주문 데이터 접근 계층 추상화
 *
 * 【 Repository 패턴 】
 * - 데이터 접근 로직을 캡슐화하여 비즈니스 로직과 분리
 * - 인터페이스로 추상화 → ORM 교체 용이 (NuVatis ↔ Dapper ↔ EF Core)
 * - 테스트 용이성 향상 (Mock Repository 사용)
 *
 * 【 벤치마크 시나리오 】
 * - Simple: GetById, WhereClause, SimplePaging, InsertSingle
 * - Medium: TwoThreeJoin, GroupByAggregate, DynamicSearch, Transaction
 * - Complex: FivePlusJoin, NPlusOneProblem, KeysetPaging
 * - Stress: BulkWrite50K, ComplexAggregation
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public interface IOrderRepository
{
    // ========================================
    // Simple: 단순 조회
    // ========================================

    /**
     * 주문 ID로 단건 조회 (Primary Key 조회)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM orders WHERE id = @id;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 예상 응답 시간: <1ms
     * - 인덱스: PRIMARY KEY (id)
     *
     * 【 사용 예시 】
     * Order? order = await repository.GetByIdAsync(12345);
     * if (order == null)
     *     throw new NotFoundException("주문을 찾을 수 없습니다");
     *
     * Console.WriteLine($"주문번호: {order.Id}");
     * Console.WriteLine($"주문금액: {order.TotalAmount:C}");
     * Console.WriteLine($"주문상태: {order.Status}");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - GetById
     */
    Task<Order?> GetByIdAsync(long id);

    /**
     * 사용자별 주문 목록 조회 (WHERE 절)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM orders
     * WHERE user_id = @userId
     * ORDER BY created_at DESC;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - user_id로 필터링
     * - 예상 응답 시간: 5-20ms (사용자당 평균 0-300건)
     * - 인덱스: idx_orders_user_created (user_id, created_at DESC)
     *
     * 【 사용 예시 】
     * var orders = await repository.GetByUserIdAsync(12345);
     * foreach (var order in orders)
     * {
     *     Console.WriteLine($"{order.Id} | {order.Status} | {order.TotalAmount:C} | {order.CreatedAt:yyyy-MM-dd}");
     * }
     *
     * 【 실전 활용 】
     * - 마이페이지: 주문 내역 조회
     * - 고객 지원: 사용자별 주문 이력 확인
     * - 통계 분석: 재구매 빈도, 구매 패턴 분석
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - WhereClause
     */
    Task<IEnumerable<Order>> GetByUserIdAsync(long userId);

    /**
     * 페이지네이션 조회 (OFFSET/LIMIT)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM orders
     * ORDER BY created_at DESC
     * OFFSET @offset LIMIT @limit;
     *
     * 【 OFFSET/LIMIT 페이징 개념 】
     * - OFFSET: 건너뛸 레코드 수 (0부터 시작)
     * - LIMIT: 반환할 최대 레코드 수
     * - 페이지 계산: OFFSET = (페이지 번호 - 1) × 페이지 크기
     *
     * 【 예시: 3페이지, 페이지당 10건 】
     * - 1페이지: OFFSET 0 LIMIT 10 (1-10번)
     * - 2페이지: OFFSET 10 LIMIT 10 (11-20번)
     * - 3페이지: OFFSET 20 LIMIT 10 (21-30번)
     *
     * 【 OFFSET 페이징의 문제점 】
     * [위험] 대용량 데이터에서 성능 저하:
     * - OFFSET 100,000 → DB가 100,000건을 읽고 버림
     * - 시간 복잡도: O(OFFSET + LIMIT)
     * - 예: OFFSET 1,000,000 시 1초 이상 소요 가능
     *
     * [권장] Keyset Pagination 대안:
     * - 마지막 ID 기반 페이징 (GetKeysetPagedAsync 참조)
     * - OFFSET 없이 WHERE id > @lastId LIMIT @limit
     * - 시간 복잡도: O(LIMIT) - 일정한 성능
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(offset + limit)
     * - 예상 응답 시간: 2-5ms (offset < 1,000)
     * - 예상 응답 시간: 50-200ms (offset > 100,000)
     *
     * 【 사용 예시 】
     * int page = 3;
     * int pageSize = 10;
     * int offset = (page - 1) * pageSize; // 20
     *
     * var orders = await repository.GetPagedAsync(offset, pageSize);
     * Console.WriteLine($"페이지 {page} ({orders.Count()}건)");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - SimplePaging
     * - CategoryC: Complex Query - KeysetPaging (대안 비교)
     */
    Task<IEnumerable<Order>> GetPagedAsync(int offset, int limit);

    // ========================================
    // Medium: JOIN 및 집계 쿼리
    // ========================================

    /**
     * 주문 상세 조회 (2-3개 테이블 JOIN)
     *
     * 【 SQL 쿼리 】
     * SELECT o.*, u.user_name, u.email,
     *        oi.id AS order_item_id, oi.product_id, oi.quantity, oi.price
     * FROM orders o
     * JOIN users u ON o.user_id = u.id
     * LEFT JOIN order_items oi ON o.id = oi.order_id
     * WHERE o.id = @id;
     *
     * 【 Eager Loading vs Lazy Loading 】
     * [비권장] Lazy Loading (N+1 문제):
     *   Order order = GetById(123);
     *   string userName = order.User.UserName; // 추가 쿼리 발생!
     *   foreach (var item in order.OrderItems) { ... } // 추가 쿼리 발생!
     *   총 쿼리: 1 (Order) + 1 (User) + 1 (OrderItems) = 3번
     *
     * [권장] Eager Loading (JOIN 사용):
     *   Order order = GetWithUserAndItemsAsync(123);
     *   → 1번의 JOIN 쿼리로 모두 로드
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - PK + FK Index 사용
     * - 예상 응답 시간: 2-5ms
     * - 네트워크 왕복: 1회 (vs Lazy Loading 3회)
     *
     * 【 사용 예시 】
     * Order? order = await repository.GetWithUserAndItemsAsync(12345);
     * Console.WriteLine($"주문자: {order?.User?.UserName}");
     * Console.WriteLine($"주문 항목 수: {order?.OrderItems?.Count ?? 0}");
     *
     * foreach (var item in order?.OrderItems ?? Enumerable.Empty<OrderItem>())
     * {
     *     Console.WriteLine($"  - 상품 ID: {item.ProductId}, 수량: {item.Quantity}, 가격: {item.Price:C}");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - TwoThreeJoin
     */
    Task<Order?> GetWithUserAndItemsAsync(long id);

    /**
     * 주문 상태별 총 금액 집계 (GROUP BY + SUM)
     *
     * 【 SQL 쿼리 】
     * SELECT status,
     *        SUM(total_amount) AS total_sales
     * FROM orders
     * GROUP BY status
     * ORDER BY total_sales DESC;
     *
     * 【 GROUP BY 개념 】
     * - 동일한 status 값을 가진 행들을 그룹화
     * - 각 그룹에 대해 집계 함수 실행 (SUM, COUNT, AVG 등)
     *
     * 【 집계 함수 종류 】
     * - SUM(total_amount): 총합
     * - COUNT(*): 건수
     * - AVG(total_amount): 평균
     * - MAX(total_amount): 최댓값
     * - MIN(total_amount): 최솟값
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - 전체 주문 스캔
     * - 예상 응답 시간: 50-200ms (1천만 건 주문)
     * - 인덱스: idx_orders_status (status 컬럼)
     *
     * 【 사용 예시 】
     * var salesByStatus = await repository.GetTotalAmountByStatusAsync();
     * foreach (var (status, totalSales) in salesByStatus)
     * {
     *     Console.WriteLine($"{status}: {totalSales:C}");
     * }
     *
     * 출력 예시:
     * completed: ₩500,000,000
     * pending: ₩50,000,000
     * cancelled: ₩10,000,000
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - GroupByAggregate
     */
    Task<Dictionary<string, decimal>> GetTotalAmountByStatusAsync();

    /**
     * 동적 검색 (MyBatis 스타일 동적 SQL)
     *
     * 【 SQL 쿼리 (동적) 】
     * SELECT * FROM orders
     * WHERE 1=1
     *   AND (@userId IS NULL OR user_id = @userId)
     *   AND (@status IS NULL OR status = @status)
     *   AND (@fromDate IS NULL OR created_at >= @fromDate)
     *   AND (@toDate IS NULL OR created_at <= @toDate)
     * ORDER BY created_at DESC;
     *
     * 【 동적 SQL 개념 】
     * - 파라미터 값에 따라 SQL 쿼리 동적 생성
     * - MyBatis XML: <if test="userId != null">...</if>
     * - NuVatis XML: 동일한 <if> 문법 지원
     *
     * 【 SQL NULL 처리 패턴 】
     * - @userId IS NULL: 파라미터가 null이면 조건 무시
     * - OR user_id = @userId: 파라미터 값이 있으면 조건 적용
     *
     * 【 MyBatis/NuVatis XML 예시 】
     * <select id="SearchAsync" resultType="Order">
     *   SELECT * FROM orders
     *   WHERE 1=1
     *   <if test="userId != null">
     *     AND user_id = #{userId}
     *   </if>
     *   <if test="status != null">
     *     AND status = #{status}
     *   </if>
     *   <if test="fromDate != null">
     *     AND created_at >= #{fromDate}
     *   </if>
     *   <if test="toDate != null">
     *     AND created_at <= #{toDate}
     *   </if>
     *   ORDER BY created_at DESC
     * </select>
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - 조건에 따라 스캔 범위 변동
     * - 예상 응답 시간: 5-50ms (조건에 따라 다름)
     * - 인덱스 활용: user_id, status, created_at 복합 인덱스
     *
     * 【 사용 예시 】
     * // 사용자 ID만 지정
     * var orders1 = await repository.SearchAsync(userId: 12345, null, null, null);
     *
     * // 상태와 날짜 범위 지정
     * var orders2 = await repository.SearchAsync(
     *     userId: null,
     *     status: "completed",
     *     fromDate: new DateTime(2026, 1, 1),
     *     toDate: new DateTime(2026, 12, 31)
     * );
     *
     * // 모든 조건 지정
     * var orders3 = await repository.SearchAsync(
     *     userId: 12345,
     *     status: "pending",
     *     fromDate: DateTime.Now.AddDays(-30),
     *     toDate: DateTime.Now
     * );
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - DynamicSearch
     */
    Task<IEnumerable<Order>> SearchAsync(long? userId, string? status, DateTime? fromDate, DateTime? toDate);

    /**
     * 트랜잭션 내 복합 작업 (Order + OrderItems + Payment + Shipment)
     *
     * 【 SQL 쿼리 (트랜잭션) 】
     * BEGIN TRANSACTION;
     *
     * -- 1. 주문 생성
     * INSERT INTO orders (user_id, total_amount, status, created_at)
     * VALUES (@userId, @totalAmount, 'pending', NOW())
     * RETURNING id;
     *
     * -- 2. 주문 항목 생성 (여러 건)
     * INSERT INTO order_items (order_id, product_id, quantity, price, total_price)
     * VALUES (@orderId, @productId1, @quantity1, @price1, @totalPrice1),
     *        (@orderId, @productId2, @quantity2, @price2, @totalPrice2);
     *
     * -- 3. 결제 정보 생성
     * INSERT INTO payments (order_id, payment_method, amount, status)
     * VALUES (@orderId, @paymentMethod, @amount, 'pending');
     *
     * -- 4. 배송 정보 생성
     * INSERT INTO shipments (order_id, address, status)
     * VALUES (@orderId, @address, 'pending');
     *
     * COMMIT;
     *
     * 【 트랜잭션(Transaction) 개념 】
     * - 원자성(Atomicity): 모두 성공 또는 모두 실패 (부분 성공 없음)
     * - 일관성(Consistency): 데이터 무결성 유지
     * - 격리성(Isolation): 동시 실행 트랜잭션 간 간섭 없음
     * - 지속성(Durability): 커밋 후 영구 저장
     *
     * 【 트랜잭션 필요성 】
     * [위험] 트랜잭션 없이 실행:
     *   InsertOrder() ✓
     *   InsertOrderItems() ✓
     *   InsertPayment() ✗ 실패
     *   InsertShipment() 실행 안 됨
     *   → 주문과 항목만 저장, 결제/배송 정보 없음 (데이터 불일치)
     *
     * [안전] 트랜잭션 사용:
     *   BEGIN;
     *   InsertOrder() ✓
     *   InsertOrderItems() ✓
     *   InsertPayment() ✗ 실패
     *   ROLLBACK; → 모든 작업 취소 (데이터 일관성 유지)
     *
     * 【 .NET 트랜잭션 패턴 】
     * using var transaction = await connection.BeginTransactionAsync();
     * try
     * {
     *     long orderId = await InsertOrderAsync(order, transaction);
     *     await InsertOrderItemsAsync(orderId, items, transaction);
     *     await InsertPaymentAsync(orderId, payment, transaction);
     *     await InsertShipmentAsync(orderId, shipment, transaction);
     *
     *     await transaction.CommitAsync();
     *     return orderId;
     * }
     * catch
     * {
     *     await transaction.RollbackAsync();
     *     throw;
     * }
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - 단일 트랜잭션 내 4개 INSERT
     * - 예상 응답 시간: 10-30ms
     * - 네트워크 왕복: 1회 (트랜잭션 내 모든 작업)
     *
     * 【 사용 예시 】
     * Order order = new Order
     * {
     *     UserId = 12345,
     *     TotalAmount = 50000,
     *     Status = "pending",
     *     CreatedAt = DateTime.UtcNow
     * };
     *
     * List<OrderItem> items = new List<OrderItem>
     * {
     *     new() { ProductId = 123, Quantity = 2, Price = 10000, TotalPrice = 20000 },
     *     new() { ProductId = 456, Quantity = 3, Price = 10000, TotalPrice = 30000 }
     * };
     *
     * Payment payment = new Payment
     * {
     *     PaymentMethod = "credit_card",
     *     Amount = 50000,
     *     Status = "pending"
     * };
     *
     * Shipment shipment = new Shipment
     * {
     *     Address = "서울시 강남구...",
     *     Status = "pending"
     * };
     *
     * long orderId = await repository.CreateCompleteOrderAsync(order, items, payment, shipment);
     * Console.WriteLine($"주문 생성 완료: {orderId}");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - Transaction
     */
    Task<long> CreateCompleteOrderAsync(Order order, IEnumerable<OrderItem> items, Payment payment, Shipment shipment);

    // ========================================
    // Complex: 고급 쿼리
    // ========================================

    /**
     * 주문 완전 조회 (5개 이상 테이블 JOIN)
     *
     * 【 SQL 쿼리 】
     * SELECT o.*, u.user_name, u.email,
     *        oi.*, p.product_name, p.price AS product_price,
     *        pay.payment_method, pay.status AS payment_status,
     *        s.address, s.status AS shipment_status
     * FROM orders o
     * JOIN users u ON o.user_id = u.id
     * LEFT JOIN order_items oi ON o.id = oi.order_id
     * LEFT JOIN products p ON oi.product_id = p.id
     * LEFT JOIN payments pay ON o.id = pay.order_id
     * LEFT JOIN shipments s ON o.id = s.order_id
     * WHERE o.id = @id;
     *
     * 【 JOIN 종류 】
     * - INNER JOIN: 양쪽 테이블에 매칭되는 행만 반환
     * - LEFT JOIN: 왼쪽 테이블의 모든 행 반환 (오른쪽 NULL 가능)
     * - RIGHT JOIN: 오른쪽 테이블의 모든 행 반환 (왼쪽 NULL 가능)
     * - FULL JOIN: 양쪽 테이블의 모든 행 반환 (매칭 없어도 포함)
     *
     * 【 LEFT JOIN 사용 이유 】
     * - order_items: 주문에 항목이 없을 수도 있음
     * - products: 항목이 있으면 상품 정보 포함
     * - payments: 결제 전 주문 (NULL 가능)
     * - shipments: 배송 전 주문 (NULL 가능)
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - PK + FK Index 사용
     * - 예상 응답 시간: 10-30ms (JOIN 5개)
     * - 네트워크 왕복: 1회
     *
     * 【 사용 예시 】
     * Order? order = await repository.GetCompleteOrderAsync(12345);
     * Console.WriteLine($"주문자: {order?.User?.UserName}");
     * Console.WriteLine($"주문 항목 수: {order?.OrderItems?.Count ?? 0}");
     * Console.WriteLine($"결제 수단: {order?.Payment?.PaymentMethod}");
     * Console.WriteLine($"배송 주소: {order?.Shipment?.Address}");
     *
     * foreach (var item in order?.OrderItems ?? Enumerable.Empty<OrderItem>())
     * {
     *     Console.WriteLine($"  - {item.Product?.ProductName} x {item.Quantity} = {item.TotalPrice:C}");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - FivePlusJoin
     */
    Task<Order?> GetCompleteOrderAsync(long id);

    /**
     * N+1 문제 시뮬레이션 (잘못된 방식 - Lazy Loading)
     *
     * 【 N+1 문제란? 】
     * - 1번의 쿼리로 N개의 주문을 조회
     * - 각 주문마다 1번씩 추가 쿼리 (사용자, 항목 등)
     * - 총 쿼리: 1 + N + N + ... = 1 + (N × 관계 수)
     *
     * 【 잘못된 방식 (N+1 발생) 】
     * // 1. 주문 10건 조회 (1번 쿼리)
     * SELECT * FROM orders WHERE user_id = 12345 LIMIT 10;
     *
     * // 2. 각 주문마다 사용자 조회 (10번 쿼리)
     * SELECT * FROM users WHERE id = @userId1;
     * SELECT * FROM users WHERE id = @userId2;
     * ...
     * SELECT * FROM users WHERE id = @userId10;
     *
     * // 3. 각 주문마다 항목 조회 (10번 쿼리)
     * SELECT * FROM order_items WHERE order_id = @orderId1;
     * SELECT * FROM order_items WHERE order_id = @orderId2;
     * ...
     * SELECT * FROM order_items WHERE order_id = @orderId10;
     *
     * 총 쿼리: 1 + 10 + 10 = 21번
     *
     * 【 .NET Lazy Loading 예시 】
     * var orders = await GetOrdersAsync(userId: 12345, limit: 10);
     * foreach (var order in orders)
     * {
     *     // [위험] 각 반복마다 추가 쿼리 발생
     *     Console.WriteLine($"주문자: {order.User.UserName}"); // 쿼리 1번
     *     Console.WriteLine($"항목 수: {order.OrderItems.Count}"); // 쿼리 1번
     * }
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(N × 관계 수) - 선형 증가
     * - 예상 응답 시간: 100-500ms (10건 기준)
     * - 네트워크 왕복: 21회 (1 + 10 + 10)
     *
     * 【 사용 예시 (성능 측정용) 】
     * var orders = await repository.GetOrdersWithNPlusOneProblemAsync(12345, 10);
     * foreach (var order in orders)
     * {
     *     Console.WriteLine($"{order.Id} | {order.User?.UserName} | {order.OrderItems?.Count ?? 0}개 항목");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - NPlusOneProblem (안티패턴 측정)
     */
    Task<IEnumerable<Order>> GetOrdersWithNPlusOneProblemAsync(long userId, int limit);

    /**
     * N+1 문제 해결 (올바른 방식 - Eager Loading)
     *
     * 【 올바른 방식 (1번 쿼리) 】
     * SELECT o.*, u.user_name, u.email,
     *        oi.id AS order_item_id, oi.product_id, oi.quantity
     * FROM orders o
     * JOIN users u ON o.user_id = u.id
     * LEFT JOIN order_items oi ON o.id = oi.order_id
     * WHERE o.user_id = 12345
     * ORDER BY o.created_at DESC
     * LIMIT 10;
     *
     * 총 쿼리: 1번 (vs N+1 방식 21번)
     *
     * 【 Eager Loading 개념 】
     * - 관계된 데이터를 미리 JOIN하여 한 번에 로드
     * - EF Core: .Include(o => o.User).Include(o => o.OrderItems)
     * - NuVatis: XML 매퍼에서 <collection> 사용
     *
     * 【 성능 비교 】
     * [비권장] Lazy Loading (N+1):
     *   - 쿼리: 21번 (1 + 10 + 10)
     *   - 응답 시간: 100-500ms
     *   - 네트워크 왕복: 21회
     *
     * [권장] Eager Loading (JOIN):
     *   - 쿼리: 1번
     *   - 응답 시간: 5-20ms
     *   - 네트워크 왕복: 1회
     *
     * 성능 개선: 5-25배 빠름
     *
     * 【 .NET Eager Loading 예시 】
     * // EF Core
     * var orders = await context.Orders
     *     .Where(o => o.UserId == 12345)
     *     .Include(o => o.User)
     *     .Include(o => o.OrderItems)
     *     .OrderByDescending(o => o.CreatedAt)
     *     .Take(10)
     *     .ToListAsync();
     *
     * // NuVatis XML 매퍼
     * <select id="GetOrdersOptimizedAsync" resultMap="OrderWithRelationsMap">
     *   SELECT o.*, u.*, oi.*
     *   FROM orders o
     *   JOIN users u ON o.user_id = u.id
     *   LEFT JOIN order_items oi ON o.id = oi.order_id
     *   WHERE o.user_id = #{userId}
     *   ORDER BY o.created_at DESC
     *   LIMIT #{limit}
     * </select>
     *
     * 【 사용 예시 】
     * var orders = await repository.GetOrdersOptimizedAsync(12345, 10);
     * foreach (var order in orders)
     * {
     *     // [안전] 추가 쿼리 없음 (이미 로드됨)
     *     Console.WriteLine($"{order.Id} | {order.User?.UserName} | {order.OrderItems?.Count ?? 0}개 항목");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - NPlusOneProblem (최적화된 방식)
     */
    Task<IEnumerable<Order>> GetOrdersOptimizedAsync(long userId, int limit);

    /**
     * Keyset 페이징 (OFFSET 대신 마지막 ID 사용)
     *
     * 【 SQL 쿼리 】
     * -- 첫 페이지 (lastId = null)
     * SELECT * FROM orders
     * ORDER BY id DESC
     * LIMIT @limit;
     *
     * -- 다음 페이지 (lastId = 12345)
     * SELECT * FROM orders
     * WHERE id < @lastId
     * ORDER BY id DESC
     * LIMIT @limit;
     *
     * 【 Keyset Pagination 개념 】
     * - OFFSET 대신 마지막 레코드의 ID를 기준으로 다음 페이지 조회
     * - WHERE id < @lastId → 이전에 본 레코드 이후부터 조회
     * - 시간 복잡도: O(LIMIT) - OFFSET과 무관하게 일정한 성능
     *
     * 【 OFFSET vs Keyset 비교 】
     * [비권장] OFFSET 페이징:
     *   -- 1,000,000번째 페이지 조회
     *   SELECT * FROM orders
     *   ORDER BY id DESC
     *   OFFSET 1000000 LIMIT 10;
     *
     *   → DB가 1,000,000건을 읽고 버림
     *   → 응답 시간: 1-5초
     *
     * [권장] Keyset 페이징:
     *   -- 마지막 ID = 5000000
     *   SELECT * FROM orders
     *   WHERE id < 5000000
     *   ORDER BY id DESC
     *   LIMIT 10;
     *
     *   → DB가 10건만 읽음
     *   → 응답 시간: 2-10ms (일정)
     *
     * 성능 개선: 100-500배 빠름 (대용량 데이터)
     *
     * 【 Keyset 페이징 제약 사항 】
     * - 정렬 기준이 고유해야 함 (id, created_at 등)
     * - 임의 페이지 이동 불가 ("5페이지로 이동" 불가능)
     * - "다음", "이전" 버튼만 가능
     * - 무한 스크롤, 피드 등에 적합
     *
     * 【 사용 예시 】
     * // 첫 페이지 (10건)
     * var firstPage = await repository.GetKeysetPagedAsync(lastId: null, limit: 10);
     * long? lastId = firstPage.LastOrDefault()?.Id;
     *
     * // 다음 페이지 (10건)
     * var secondPage = await repository.GetKeysetPagedAsync(lastId: lastId, limit: 10);
     * lastId = secondPage.LastOrDefault()?.Id;
     *
     * // 계속 반복 (무한 스크롤)
     * var thirdPage = await repository.GetKeysetPagedAsync(lastId: lastId, limit: 10);
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - KeysetPaging (vs OFFSET 비교)
     */
    Task<IEnumerable<Order>> GetKeysetPagedAsync(long? lastId, int limit);

    // ========================================
    // Simple: 삽입
    // ========================================

    /**
     * 주문 단건 삽입 (INSERT)
     *
     * 【 SQL 쿼리 】
     * INSERT INTO orders (user_id, total_amount, status, created_at)
     * VALUES (@userId, @totalAmount, @status, @createdAt)
     * RETURNING id;
     *
     * 【 RETURNING 절 (PostgreSQL) 】
     * - INSERT 후 생성된 ID를 즉시 반환
     * - MySQL: LAST_INSERT_ID() 함수 사용
     * - SQL Server: SCOPE_IDENTITY() 함수 사용
     *
     * 【 .NET RETURNING 패턴 】
     * // PostgreSQL (NuVatis/Dapper)
     * long id = await connection.ExecuteScalarAsync<long>(
     *     "INSERT INTO orders (...) VALUES (...) RETURNING id"
     * );
     *
     * // MySQL
     * await connection.ExecuteAsync("INSERT INTO orders (...) VALUES (...)");
     * long id = await connection.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1)
     * - 예상 응답 시간: 2-5ms
     *
     * 【 사용 예시 】
     * Order order = new Order
     * {
     *     UserId = 12345,
     *     TotalAmount = 50000,
     *     Status = "pending",
     *     CreatedAt = DateTime.UtcNow
     * };
     *
     * long orderId = await repository.InsertAsync(order);
     * Console.WriteLine($"주문 생성 완료: {orderId}");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - InsertSingle
     */
    Task<long> InsertAsync(Order order);

    // ========================================
    // Stress: 대용량 데이터 처리
    // ========================================

    /**
     * 대량 주문 및 항목 삽입 (50K 건)
     *
     * 【 SQL 쿼리 (PostgreSQL COPY) 】
     * -- 1. 주문 대량 삽입 (COPY 프로토콜)
     * COPY orders (user_id, total_amount, status, created_at)
     * FROM STDIN BINARY;
     *
     * -- 2. 주문 항목 대량 삽입 (COPY 프로토콜)
     * COPY order_items (order_id, product_id, quantity, price, total_price)
     * FROM STDIN BINARY;
     *
     * 【 대량 삽입 방법 비교 】
     * [비권장] 개별 INSERT (50K 번 실행):
     *   foreach (var order in orders)
     *   {
     *       await connection.ExecuteAsync("INSERT INTO orders (...) VALUES (...)");
     *   }
     *   → 응답 시간: 50-200초 (네트워크 왕복 50,000회)
     *
     * [권장] 배치 INSERT (1,000개씩 묶음):
     *   INSERT INTO orders (...) VALUES
     *   (...), (...), (...), ... -- 1,000개
     *   → 응답 시간: 5-20초 (네트워크 왕복 50회)
     *
     * [최적] PostgreSQL COPY 프로토콜:
     *   using var writer = connection.BeginBinaryImport("COPY orders ...");
     *   foreach (var order in orders)
     *   {
     *       writer.StartRow();
     *       writer.Write(order.UserId);
     *       writer.Write(order.TotalAmount);
     *       // ...
     *   }
     *   writer.Complete();
     *   → 응답 시간: 1-5초 (바이너리 전송)
     *
     * 성능 개선: 10-40배 빠름
     *
     * 【 .NET COPY 프로토콜 예시 】
     * using var writer = connection.BeginBinaryImport(
     *     "COPY orders (user_id, total_amount, status, created_at) FROM STDIN BINARY"
     * );
     *
     * foreach (var (order, items) in orderData)
     * {
     *     writer.StartRow();
     *     writer.Write(order.UserId);
     *     writer.Write(order.TotalAmount);
     *     writer.Write(order.Status);
     *     writer.Write(order.CreatedAt);
     * }
     *
     * long rowsInserted = writer.Complete();
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n)
     * - 예상 응답 시간: 1-5초 (50K 건)
     * - 네트워크 전송: 바이너리 프로토콜 (압축)
     *
     * 【 사용 예시 】
     * List<(Order order, IEnumerable<OrderItem> items)> orderData = GenerateOrders(50000);
     * int totalInserted = await repository.BulkInsertOrdersWithItemsAsync(orderData);
     * Console.WriteLine($"총 {totalInserted}건 삽입 완료");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryD: Stress - BulkWrite50K
     */
    Task<int> BulkInsertOrdersWithItemsAsync(IEnumerable<(Order order, IEnumerable<OrderItem> items)> orderData);

    /**
     * 일별 매출 집계 (복합 집계 쿼리)
     *
     * 【 SQL 쿼리 】
     * SELECT DATE(created_at) AS sale_date,
     *        COUNT(DISTINCT id) AS order_count,
     *        COUNT(DISTINCT user_id) AS customer_count,
     *        SUM(total_amount) AS total_sales,
     *        AVG(total_amount) AS avg_order_value,
     *        MAX(total_amount) AS max_order,
     *        MIN(total_amount) AS min_order
     * FROM orders
     * WHERE created_at BETWEEN @fromDate AND @toDate
     *   AND status = 'completed'
     * GROUP BY DATE(created_at)
     * ORDER BY sale_date DESC;
     *
     * 【 집계 함수 조합 】
     * - COUNT(DISTINCT id): 주문 건수
     * - COUNT(DISTINCT user_id): 고객 수 (중복 제거)
     * - SUM(total_amount): 총 매출
     * - AVG(total_amount): 평균 주문 금액
     * - MAX(total_amount): 최대 주문 금액
     * - MIN(total_amount): 최소 주문 금액
     *
     * 【 DATE() 함수 】
     * - TIMESTAMP를 DATE로 변환 (시간 제거)
     * - 예: '2026-03-01 14:30:00' → '2026-03-01'
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - 날짜 범위 내 주문 스캔
     * - 예상 응답 시간: 50-500ms (1년치 데이터)
     * - 인덱스: idx_orders_created_status (created_at, status)
     *
     * 【 사용 예시 】
     * DateTime fromDate = new DateTime(2026, 1, 1);
     * DateTime toDate = new DateTime(2026, 12, 31);
     *
     * var dailySales = await repository.GetDailySalesAggregationAsync(fromDate, toDate);
     * foreach (var sale in dailySales)
     * {
     *     Console.WriteLine($"{sale.sale_date:yyyy-MM-dd} | " +
     *                       $"주문: {sale.order_count}건 | " +
     *                       $"고객: {sale.customer_count}명 | " +
     *                       $"매출: {sale.total_sales:C} | " +
     *                       $"평균: {sale.avg_order_value:C}");
     * }
     *
     * 출력 예시:
     * 2026-03-01 | 주문: 1,234건 | 고객: 987명 | 매출: ₩123,456,789 | 평균: ₩100,047
     * 2026-02-28 | 주문: 1,156건 | 고객: 923명 | 매출: ₩115,623,000 | 평균: ₩100,019
     *
     * 【 벤치마크 시나리오 】
     * - CategoryD: Stress - ComplexAggregation
     */
    Task<IEnumerable<dynamic>> GetDailySalesAggregationAsync(DateTime fromDate, DateTime toDate);
}
