namespace NuVatis.Benchmark.Core.Models;

/**
 * 위시리스트 엔티티 (Entity) - 찜 목록 (N:M 중간 테이블)
 *
 * 【 테이블 정보 】
 * - 테이블명: wishlists
 * - 레코드 수: 2,000,000개 (사용자별 찜한 상품)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key:
 *   - user_id → users.id
 *   - product_id → products.id
 * - 복합 UNIQUE: (user_id, product_id) - 중복 찜 방지
 *
 * 【 N:M 관계 】
 * - users (N) ↔ wishlists (중간 테이블) ↔ products (M)
 * - 한 사용자는 여러 상품을 찜할 수 있음
 * - 한 상품은 여러 사용자에게 찜될 수 있음
 *
 * 【 비즈니스 활용 】
 * - 재구매 유도: 찜 상품 할인 알림
 * - 인기 상품 분석: 많이 찜된 상품 = 인기 상품
 * - 개인화 추천: 찜 목록 기반 상품 추천
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Wishlist
{
    /**
     * 위시리스트 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     */
    public long Id { get; set; }

    /**
     * 사용자 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: user_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (user_id) REFERENCES users(id)
     * - INDEX: idx_wishlists_user_id (사용자별 찜 목록 조회)
     *
     * 【 복합 UNIQUE 제약 】
     * CREATE UNIQUE INDEX idx_wishlists_user_product
     * ON wishlists(user_id, product_id);
     * → 동일 사용자가 동일 상품을 2번 이상 찜하지 못하도록 방지
     *
     * 【 SQL 예시 】
     * -- 사용자의 찜 목록 조회
     * SELECT w.*, p.product_name, p.price, pi.image_url
     * FROM wishlists w
     * JOIN products p ON w.product_id = p.id
     * LEFT JOIN product_images pi ON p.id = pi.product_id AND pi.is_primary = TRUE
     * WHERE w.user_id = 12345
     * ORDER BY w.created_at DESC;
     *
     * 【 찜 토글 로직 】
     * -- 찜 추가 (이미 있으면 오류)
     * INSERT INTO wishlists (user_id, product_id, created_at)
     * VALUES (12345, 123, NOW());
     *
     * -- 찜 제거
     * DELETE FROM wishlists
     * WHERE user_id = 12345 AND product_id = 123;
     *
     * -- 찜 토글 (있으면 제거, 없으면 추가)
     * BEGIN;
     * DELETE FROM wishlists
     * WHERE user_id = 12345 AND product_id = 123;
     *
     * IF (ROW_COUNT() = 0) THEN
     *     INSERT INTO wishlists (user_id, product_id, created_at)
     *     VALUES (12345, 123, NOW());
     * END IF;
     * COMMIT;
     */
    public long UserId { get; set; }

    /**
     * 상품 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: product_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (product_id) REFERENCES products(id)
     * - INDEX: idx_wishlists_product_id (상품별 찜 개수 조회)
     *
     * 【 SQL 예시 】
     * -- 상품별 찜 개수 (인기 상품 순위)
     * SELECT p.product_name,
     *        COUNT(w.id) AS wishlist_count
     * FROM products p
     * LEFT JOIN wishlists w ON p.id = w.product_id
     * WHERE p.is_active = TRUE
     * GROUP BY p.id, p.product_name
     * ORDER BY wishlist_count DESC
     * LIMIT 10; -- TOP 10 인기 상품
     *
     * 【 찜 여부 확인 】
     * -- 사용자가 특정 상품을 찜했는지 확인
     * SELECT EXISTS (
     *     SELECT 1 FROM wishlists
     *     WHERE user_id = 12345 AND product_id = 123
     * ) AS is_wishlisted;
     *
     * 결과: TRUE (찜함) or FALSE (찜 안 함)
     *
     * 【 실전 활용 (상품 목록) 】
     * -- 상품 목록 + 현재 사용자의 찜 여부
     * SELECT p.*,
     *        EXISTS (
     *            SELECT 1 FROM wishlists
     *            WHERE user_id = 12345 AND product_id = p.id
     *        ) AS is_wishlisted
     * FROM products p
     * WHERE p.category_id = 10
     *   AND p.is_active = TRUE
     * ORDER BY p.created_at DESC
     * LIMIT 20;
     */
    public long ProductId { get; set; }

    /**
     * 생성 일시 (찜한 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     * - INDEX: idx_wishlists_created_at (최근 찜 순 조회)
     *
     * 【 실전 활용 】
     * -- 최근 7일 내 많이 찜된 상품 (트렌드)
     * SELECT p.product_name,
     *        COUNT(w.id) AS recent_wishlist_count
     * FROM wishlists w
     * JOIN products p ON w.product_id = p.id
     * WHERE w.created_at >= NOW() - INTERVAL '7 days'
     * GROUP BY p.id, p.product_name
     * ORDER BY recent_wishlist_count DESC
     * LIMIT 10;
     *
     * 【 마케팅 활용 】
     * -- 찜 후 미구매 사용자 (리타게팅 대상)
     * SELECT DISTINCT w.user_id, u.email
     * FROM wishlists w
     * JOIN users u ON w.user_id = u.id
     * LEFT JOIN orders o ON w.user_id = o.user_id
     *     AND o.created_at > w.created_at
     * WHERE w.created_at >= NOW() - INTERVAL '7 days'
     *   AND w.created_at < NOW() - INTERVAL '1 day'
     *   AND o.id IS NULL; -- 찜 후 주문 없음
     *
     * → 찜 후 1~7일 경과했지만 구매하지 않은 사용자 (할인 쿠폰 발송)
     *
     * 【 찜 → 구매 전환율 】
     * -- 찜 후 실제 구매한 비율
     * SELECT
     *   COUNT(DISTINCT w.id) AS total_wishlists,
     *   COUNT(DISTINCT CASE
     *       WHEN EXISTS (
     *           SELECT 1 FROM order_items oi
     *           JOIN orders o ON oi.order_id = o.id
     *           WHERE o.user_id = w.user_id
     *             AND oi.product_id = w.product_id
     *             AND o.created_at > w.created_at
     *       ) THEN w.id
     *   END) AS purchased_wishlists,
     *   ROUND(COUNT(DISTINCT CASE ... END) * 100.0 / COUNT(DISTINCT w.id), 2) AS conversion_rate
     * FROM wishlists w
     * WHERE w.created_at >= NOW() - INTERVAL '30 days';
     *
     * 결과: conversion_rate = 15.50% (찜 → 구매 전환율 15.5%)
     */
    public DateTime CreatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 사용자 정보 (N:1 관계)
     *
     * 【 관계 】
     * - wishlists (N) → users (1)
     * - 여러 찜 기록이 한 사용자에게 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT w.*, u.user_name, u.email
     * FROM wishlists w
     * JOIN users u ON w.user_id = u.id
     * WHERE w.id = 12345;
     */
    public User? User { get; set; }

    /**
     * 상품 정보 (N:1 관계)
     *
     * 【 관계 】
     * - wishlists (N) → products (1)
     * - 여러 찜 기록이 한 상품을 참조
     *
     * 【 SQL JOIN 예시 】
     * SELECT w.*, p.product_name, p.price, p.stock_quantity
     * FROM wishlists w
     * JOIN products p ON w.product_id = p.id
     * WHERE w.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Wishlist wishlist = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"사용자: {wishlist.User?.UserName}");
     * Console.WriteLine($"상품명: {wishlist.Product?.ProductName}");
     * Console.WriteLine($"가격: {wishlist.Product?.Price:C}");
     * Console.WriteLine($"찜한 날짜: {wishlist.CreatedAt:yyyy-MM-dd}");
     *
     * 【 찜 목록 표시 (마이페이지) 】
     * User user = await userRepository.GetByIdAsync(12345);
     * var wishlists = user.Wishlists?
     *     .OrderByDescending(w => w.CreatedAt)
     *     .Take(10);
     *
     * foreach (var w in wishlists ?? Enumerable.Empty<Wishlist>())
     * {
     *     Console.WriteLine($"{w.Product?.ProductName} - {w.Product?.Price:C}");
     *
     *     // 재고 확인
     *     if (w.Product?.StockQuantity == 0)
     *         Console.WriteLine("  [품절]");
     *     else if (w.Product?.StockQuantity < 10)
     *         Console.WriteLine($"  [재고 {w.Product.StockQuantity}개 남음]");
     * }
     */
    public Product? Product { get; set; }
}
