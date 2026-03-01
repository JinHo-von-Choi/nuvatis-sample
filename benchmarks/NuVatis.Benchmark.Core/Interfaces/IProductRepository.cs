using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Core.Interfaces;

/**
 * 상품 Repository 인터페이스 - 상품 데이터 접근 계층 추상화
 *
 * 【 Repository 패턴 】
 * - 데이터 접근 로직을 캡슐화하여 비즈니스 로직과 분리
 * - 인터페이스로 추상화 → ORM 교체 용이 (NuVatis ↔ Dapper ↔ EF Core)
 * - 테스트 용이성 향상 (Mock Repository 사용)
 *
 * 【 벤치마크 시나리오 】
 * - Simple: GetById, WhereClause, UpdateSingle
 * - Medium: TwoThreeJoin, GroupByAggregate
 * - Complex: WindowFunctions
 * - Stress: Query100K (전체 활성 상품 조회)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public interface IProductRepository
{
    // ========================================
    // Simple: 단순 조회
    // ========================================

    /**
     * 상품 ID로 단건 조회 (Primary Key 조회)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM products WHERE id = @id;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 예상 응답 시간: <1ms
     * - 인덱스: PRIMARY KEY (id)
     *
     * 【 사용 예시 】
     * Product? product = await repository.GetByIdAsync(123);
     * if (product == null)
     *     throw new NotFoundException("상품을 찾을 수 없습니다");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - GetById
     */
    Task<Product?> GetByIdAsync(long id);

    /**
     * 카테고리별 상품 목록 조회 (WHERE 절)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM products
     * WHERE category_id = @categoryId
     *   AND is_active = TRUE
     * ORDER BY created_at DESC;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - category_id로 필터링
     * - 예상 응답 시간: 5-10ms (카테고리당 평균 100개 상품)
     * - 인덱스: idx_products_category_active_created
     *
     * 【 사용 예시 】
     * var products = await repository.GetByCategoryIdAsync(10);
     * foreach (var p in products)
     * {
     *     Console.WriteLine($"{p.ProductName}: {p.Price:C}");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - WhereClause
     */
    Task<IEnumerable<Product>> GetByCategoryIdAsync(long categoryId);

    // ========================================
    // Medium: JOIN 및 집계 쿼리
    // ========================================

    /**
     * 상품 상세 조회 (2-3개 테이블 JOIN)
     *
     * 【 SQL 쿼리 】
     * SELECT p.*, c.category_name, pi.image_url, pi.is_primary
     * FROM products p
     * JOIN categories c ON p.category_id = c.id
     * LEFT JOIN product_images pi ON p.id = pi.product_id
     * WHERE p.id = @id;
     *
     * 【 Eager Loading vs Lazy Loading 】
     * [비권장] Lazy Loading (N+1 문제):
     *   Product product = GetById(123);
     *   string categoryName = product.Category.CategoryName; // 추가 쿼리 발생!
     *   foreach (var image in product.ProductImages) { ... } // 추가 쿼리 발생!
     *   총 쿼리: 1 (Product) + 1 (Category) + 1 (ProductImages) = 3번
     *
     * [권장] Eager Loading (JOIN 사용):
     *   Product product = GetWithDetailsAsync(123);
     *   → 1번의 JOIN 쿼리로 모두 로드
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - PK + FK Index 사용
     * - 예상 응답 시간: 2-5ms
     * - 네트워크 왕복: 1회
     *
     * 【 사용 예시 】
     * Product? product = await repository.GetWithDetailsAsync(123);
     * Console.WriteLine($"상품명: {product?.ProductName}");
     * Console.WriteLine($"카테고리: {product?.Category?.CategoryName}");
     * Console.WriteLine($"이미지 수: {product?.ProductImages?.Count ?? 0}");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - TwoThreeJoin
     */
    Task<Product?> GetWithDetailsAsync(long id);

    /**
     * 카테고리별 총 판매 금액 집계 (GROUP BY + JOIN)
     *
     * 【 SQL 쿼리 】
     * SELECT c.id AS category_id,
     *        SUM(oi.total_price) AS total_sales
     * FROM categories c
     * JOIN products p ON c.id = p.category_id
     * JOIN order_items oi ON p.id = oi.product_id
     * GROUP BY c.id
     * ORDER BY total_sales DESC;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - order_items 전체 스캔
     * - 예상 응답 시간: 50-100ms (5천만 건 order_items)
     * - 인덱스: idx_order_items_product_id
     *
     * 【 사용 예시 】
     * var salesByCategory = await repository.GetTotalSalesByCategoryAsync();
     * foreach (var (categoryId, totalSales) in salesByCategory)
     * {
     *     Console.WriteLine($"카테고리 {categoryId}: {totalSales:C}");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - GroupByAggregate
     */
    Task<Dictionary<long, decimal>> GetTotalSalesByCategoryAsync();

    // ========================================
    // Complex: 고급 쿼리 (Window Functions)
    // ========================================

    /**
     * 상품 가격 순위 조회 (Window Function)
     *
     * 【 SQL 쿼리 】
     * SELECT p.id,
     *        p.product_name,
     *        p.price,
     *        p.category_id,
     *        ROW_NUMBER() OVER (PARTITION BY category_id ORDER BY price DESC) AS price_rank,
     *        RANK() OVER (PARTITION BY category_id ORDER BY price DESC) AS price_rank_with_ties
     * FROM products p
     * WHERE p.is_active = TRUE
     * ORDER BY p.category_id, price_rank;
     *
     * 【 Window Function 개념 】
     * - ROW_NUMBER(): 각 파티션 내에서 순차적 번호 부여 (1, 2, 3, ...)
     * - RANK(): 동점 허용 (1, 2, 2, 4, ...) - 동점 시 다음 순위 건너뜀
     * - DENSE_RANK(): 동점 허용 (1, 2, 2, 3, ...) - 동점 시에도 순위 연속
     * - PARTITION BY: 그룹 분할 (카테고리별로 별도 순위)
     * - ORDER BY: 순위 기준 (가격 높은 순)
     *
     * 【 실전 활용 】
     * - 카테고리별 고가 상품 TOP 3
     * - 가격대별 순위 표시
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n log n) - 정렬 필요
     * - 예상 응답 시간: 10-30ms
     * - 인덱스: idx_products_category_price
     *
     * 【 사용 예시 】
     * var rankedProducts = await repository.GetProductsWithPriceRankAsync();
     * foreach (var p in rankedProducts)
     * {
     *     Console.WriteLine($"{p.category_id} | {p.product_name} | {p.price:C} | 순위: {p.price_rank}");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - WindowFunctions
     */
    Task<IEnumerable<dynamic>> GetProductsWithPriceRankAsync();

    // ========================================
    // Stress: 대용량 데이터 조회
    // ========================================

    /**
     * 모든 활성 상품 조회 (100K 건)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM products
     * WHERE is_active = TRUE
     * ORDER BY id;
     *
     * 【 대용량 데이터 처리 】
     * [비권장] ToList() - 메모리에 전체 로드:
     *   var products = await repository.GetAllActiveProductsAsync();
     *   var list = products.ToList(); // 100K 객체 메모리 로드 (100-200 MB)
     *   → OutOfMemoryException 위험
     *
     * [권장] 스트리밍 (IAsyncEnumerable):
     *   await foreach (var product in repository.GetAllActiveProductsAsyncStream())
     *   {
     *       // 한 번에 1개씩 처리 (메모리 효율적)
     *   }
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - 전체 스캔
     * - 예상 응답 시간: 500ms-2s
     * - 메모리: 100-200 MB (전체 로드 시)
     * - 네트워크 전송: 10-20 MB (압축 시)
     *
     * 【 사용 예시 】
     * var products = await repository.GetAllActiveProductsAsync();
     * int count = 0;
     * foreach (var p in products)
     * {
     *     count++;
     *     // 배치 처리 (1,000개씩)
     *     if (count % 1000 == 0)
     *         Console.WriteLine($"처리 중... {count}개");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryD: Stress - Query100K
     */
    Task<IEnumerable<Product>> GetAllActiveProductsAsync();

    // ========================================
    // Simple: 수정
    // ========================================

    /**
     * 재고 수량 업데이트 (단건 UPDATE)
     *
     * 【 SQL 쿼리 】
     * UPDATE products
     * SET stock_quantity = @newStock,
     *     updated_at = NOW()
     * WHERE id = @productId;
     *
     * 【 동시성 제어 (Concurrency Control) 】
     * [위험] Race Condition:
     *   int currentStock = GetById(123).StockQuantity; // 100
     *   int newStock = currentStock - 5; // 95
     *   UpdateStockAsync(123, newStock);
     *   → 동시에 2개 요청 시 재고 오차 발생
     *
     * [안전] Atomic Update:
     *   UPDATE products
     *   SET stock_quantity = stock_quantity - 5
     *   WHERE id = 123 AND stock_quantity >= 5;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - PK Index 사용
     * - 예상 응답 시간: 2-5ms
     *
     * 【 사용 예시 】
     * int affectedRows = await repository.UpdateStockAsync(123, 95);
     * if (affectedRows == 0)
     *     throw new ConcurrencyException("재고 업데이트 실패");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - UpdateSingle
     */
    Task<int> UpdateStockAsync(long productId, int newStock);
}
