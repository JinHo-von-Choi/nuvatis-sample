namespace NuVatis.Benchmark.Core.Models;

/**
 * 상품 엔티티 (Entity) - 데이터베이스의 products 테이블과 1:1 매핑되는 C# 클래스
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - 전자상거래 시스템의 핵심 엔티티 (상품 관리, 주문, 재고의 중심)
 *
 * 【 테이블 정보 】
 * - 테이블명: products
 * - 레코드 수: 50,000개 (5만 개 상품)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: category_id → categories.id (상품 분류)
 *
 * 【 비즈니스 중요도 】
 * - 상품 검색: 사용자가 가장 많이 조회하는 데이터
 * - 재고 관리: stock_quantity 실시간 업데이트
 * - 가격 정책: price, cost_price로 마진 계산
 * - 인기 상품: view_count, order_count로 랭킹
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Product
{
    /**
     * 상품 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가 (1, 2, 3, ...)
     *
     * 【 실전 활용 】
     * - 상품 상세 조회: SELECT * FROM products WHERE id = ?
     * - URL 파라미터: /products/12345
     */
    public long Id { get; set; }

    /**
     * 카테고리 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: category_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (category_id) REFERENCES categories(id)
     * - INDEX: idx_products_category_id (카테고리별 상품 조회 최적화)
     *
     * 【 관계 】
     * - products (N) → categories (1)
     * - 한 카테고리는 여러 상품을 가짐
     *
     * 【 SQL 예시 】
     * -- 특정 카테고리의 모든 상품 조회
     * SELECT * FROM products
     * WHERE category_id = 10
     *   AND is_active = TRUE
     * ORDER BY created_at DESC;
     *
     * -- 카테고리별 상품 수
     * SELECT c.category_name, COUNT(*) AS product_count
     * FROM categories c
     * JOIN products p ON c.id = p.category_id
     * WHERE p.is_active = TRUE
     * GROUP BY c.id, c.category_name
     * ORDER BY product_count DESC;
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_products_category_active_price
     * ON products(category_id, is_active, price);
     * → 카테고리별 활성 상품을 가격순 정렬 (상품 목록 페이지)
     */
    public long CategoryId { get; set; }

    /**
     * 상품명 (Product Name)
     *
     * 【 SQL 매핑 】
     * - 컬럼: product_name VARCHAR(200) NOT NULL
     * - INDEX: idx_products_name_fulltext (전문 검색)
     *
     * 【 전문 검색 (Full-Text Search) 】
     * CREATE INDEX idx_products_name_fulltext
     * ON products USING gin(to_tsvector('korean', product_name));
     *
     * -- 한글 검색 쿼리
     * SELECT * FROM products
     * WHERE to_tsvector('korean', product_name) @@ to_tsquery('korean', '노트북');
     * → 매우 빠른 검색 (LIKE '%노트북%'보다 100배 이상 빠름)
     *
     * 【 LIKE vs Full-Text Search 비교 】
     * [비권장] LIKE 검색:
     *   SELECT * FROM products
     *   WHERE product_name LIKE '%노트북%';
     *   → Full Table Scan (50,000건 모두 스캔) → 50-100ms
     *
     * [권장] Full-Text Search:
     *   SELECT * FROM products
     *   WHERE to_tsvector('korean', product_name) @@ to_tsquery('korean', '노트북');
     *   → GIN 인덱스 사용 → 1-5ms
     *
     * 【 실전 활용 】
     * - 상품 검색: "삼성 갤럭시 스마트폰" → "삼성", "갤럭시", "스마트폰" 토큰화
     * - 자동완성: product_name LIKE '노트북%' (앞부분 일치는 B-Tree 인덱스 사용 가능)
     */
    public string ProductName { get; set; } = string.Empty;

    /**
     * 상품 설명 (Description) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: description TEXT NULL
     *
     * 【 TEXT vs VARCHAR 】
     * - VARCHAR(N): 최대 N바이트 (예: VARCHAR(200) = 최대 200자)
     * - TEXT: 무제한 길이 (최대 1GB, PostgreSQL 기준)
     *
     * 【 언제 TEXT? 】
     * - 상품 상세 설명: 긴 텍스트 (수백~수천 자)
     * - HTML 포함: <p>, <ul>, <li> 등 마크업
     *
     * 【 언제 VARCHAR? 】
     * - 짧은 텍스트: 제목, 이름 (200자 이하)
     * - 인덱스 필요: TEXT는 인덱스 크기 제한
     *
     * 【 .NET Nullable 개념 】
     * - string?: Nullable Reference Type (C# 8.0+)
     * - null: 설명 없음 (간단한 상품)
     * - 값 있음: 상세 설명 제공
     */
    public string? Description { get; set; }

    /**
     * 판매 가격 (Price) - 고객이 결제하는 금액
     *
     * 【 SQL 매핑 】
     * - 컬럼: price DECIMAL(10, 2) NOT NULL
     * - DECIMAL(10, 2): 정수 8자리 + 소수 2자리 (최대 99,999,999.99)
     *
     * 【 .NET decimal 타입 】
     * - decimal: 128비트 고정 소수점 (재무 계산 전용)
     * - 정확한 소수점 계산 보장 (부동 소수점 오차 없음)
     *
     * 【 왜 DECIMAL(10, 2)? 】
     * - 10자리: 최대 99,999,999.99 (약 1억)
     * - 2자리 소수: 원 단위 (99.99원)
     * - 실전: 대부분의 상품 가격 커버 (1원 ~ 9,999만원)
     *
     * 【 INDEX 활용 】
     * CREATE INDEX idx_products_price ON products(price);
     * → 가격 범위 검색 최적화
     *
     * -- 10만원 이하 상품 조회
     * SELECT * FROM products
     * WHERE price <= 100000
     *   AND is_active = TRUE
     * ORDER BY price ASC;
     *
     * -- 가격대별 상품 수
     * SELECT
     *   CASE
     *     WHEN price < 10000 THEN '1만원 미만'
     *     WHEN price < 50000 THEN '1-5만원'
     *     WHEN price < 100000 THEN '5-10만원'
     *     ELSE '10만원 이상'
     *   END AS price_range,
     *   COUNT(*) AS count
     * FROM products
     * WHERE is_active = TRUE
     * GROUP BY price_range;
     */
    public decimal Price { get; set; }

    /**
     * 원가 (Cost Price) - 매입 가격, 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: cost_price DECIMAL(10, 2) NULL
     *
     * 【 .NET Nullable 개념 】
     * - decimal?: Nullable<decimal> (값이 없을 수 있음)
     * - null: 원가 비공개 (외부 판매자 상품 등)
     * - 값 있음: 원가 공개 (마진 계산 가능)
     *
     * 【 마진 계산 】
     * decimal profit = price - (costPrice ?? 0m);
     * decimal marginRate = costPrice.HasValue
     *     ? ((price - costPrice.Value) / price) × 100m
     *     : 0m;
     *
     * 예시:
     * - price: 100,000원
     * - cost_price: 70,000원
     * - profit: 30,000원
     * - margin_rate: 30%
     *
     * 【 실전 쿼리 】
     * -- 마진율 높은 상품 TOP 10
     * SELECT product_name,
     *        price,
     *        cost_price,
     *        (price - cost_price) / price × 100 AS margin_rate
     * FROM products
     * WHERE cost_price IS NOT NULL
     *   AND is_active = TRUE
     * ORDER BY margin_rate DESC
     * LIMIT 10;
     */
    public decimal? CostPrice { get; set; }

    /**
     * 재고 수량 (Stock Quantity)
     *
     * 【 SQL 매핑 】
     * - 컬럼: stock_quantity INT DEFAULT 0
     *
     * 【 .NET int 타입 】
     * - int: 32비트 정수형 (-21억 ~ +21억)
     * - 재고: 0 이상 (음수 불가)
     *
     * 【 재고 관리 】
     * - 주문 시: stock_quantity -= order_quantity (차감)
     * - 입고 시: stock_quantity += received_quantity (증가)
     * - 품절: stock_quantity = 0 (상품 목록에서 숨김 또는 "품절" 표시)
     *
     * 【 동시성 제어 (Concurrency Control) 】
     * [위험] Race Condition (경쟁 상태):
     *   -- 스레드 A: SELECT stock_quantity FROM products WHERE id = 123; -- 10개
     *   -- 스레드 B: SELECT stock_quantity FROM products WHERE id = 123; -- 10개
     *   -- 스레드 A: UPDATE products SET stock_quantity = 10 - 5 WHERE id = 123; -- 5개
     *   -- 스레드 B: UPDATE products SET stock_quantity = 10 - 3 WHERE id = 123; -- 7개 (잘못!)
     *   → 실제로는 8개 차감되어야 하는데 3개만 차감됨 (재고 오차)
     *
     * [안전] Atomic Update (원자적 갱신):
     *   UPDATE products
     *   SET stock_quantity = stock_quantity - 5
     *   WHERE id = 123
     *     AND stock_quantity >= 5; -- 재고 부족 시 UPDATE 실패
     *
     *   -- 영향받은 행 수 확인
     *   IF (ROW_COUNT() = 0) THEN
     *       RAISE EXCEPTION '재고 부족';
     *   END IF;
     *
     * 【 낙관적 잠금 (Optimistic Locking) 】
     * - version 컬럼 추가 (동시성 제어용)
     * UPDATE products
     * SET stock_quantity = stock_quantity - 5,
     *     version = version + 1
     * WHERE id = 123
     *   AND version = @expectedVersion; -- 버전 불일치 시 UPDATE 실패
     *
     * 【 실전 쿼리 】
     * -- 품절 임박 상품 조회 (재입고 필요)
     * SELECT * FROM products
     * WHERE stock_quantity <= 10
     *   AND is_active = TRUE
     * ORDER BY stock_quantity ASC;
     *
     * -- 재고 회전율 분석
     * SELECT p.product_name,
     *        p.stock_quantity,
     *        COUNT(oi.id) AS order_count
     * FROM products p
     * LEFT JOIN order_items oi ON p.id = oi.product_id
     * WHERE p.is_active = TRUE
     * GROUP BY p.id, p.product_name, p.stock_quantity
     * ORDER BY order_count DESC
     * LIMIT 10; -- 인기 상품 TOP 10
     */
    public int StockQuantity { get; set; } = 0;

    /**
     * SKU (Stock Keeping Unit) - 재고 관리 코드, 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: sku VARCHAR(50) UNIQUE NULL
     * - UNIQUE: 중복 불가 (같은 SKU 여러 상품 등록 방지)
     *
     * 【 SKU란? 】
     * - 재고 관리 단위 코드 (바코드와 유사)
     * - 예시: "LAPTOP-SAMSUNG-15-BLACK-256GB"
     * - 형식: 자유 (회사마다 다름)
     *
     * 【 실전 활용 】
     * - 바코드 스캔: SKU로 상품 조회
     *   SELECT * FROM products WHERE sku = 'LAPTOP-SAMSUNG-15-BLACK-256GB';
     *
     * - 입고 시스템: SKU로 재고 증가
     *   UPDATE products
     *   SET stock_quantity = stock_quantity + 50
     *   WHERE sku = 'LAPTOP-SAMSUNG-15-BLACK-256GB';
     *
     * 【 왜 UNIQUE? 】
     * - SKU 중복 방지 (같은 SKU로 여러 상품 등록 시 혼란)
     * - 재고 관리 시스템 통합 (바코드 스캔 시 정확한 상품 식별)
     */
    public string? Sku { get; set; }

    /**
     * 활성 상태 (true: 판매 중, false: 판매 중지)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_active BOOLEAN DEFAULT TRUE
     * - INDEX: idx_products_active (활성 상품만 조회)
     *
     * 【 소프트 삭제 (Soft Delete) 】
     * - 물리 삭제: DELETE FROM products WHERE id = 123; (복구 불가)
     * - 논리 삭제: UPDATE products SET is_active = FALSE WHERE id = 123; (복구 가능)
     *
     * 【 왜 소프트 삭제? 】
     * - 복구 가능: 실수로 삭제 시 is_active = TRUE로 복원
     * - 주문 내역 유지: 과거 주문의 상품 정보 보존
     * - 통계 유지: 총 판매량, 리뷰 수 등 유지
     *
     * 【 실전 쿼리 】
     * -- 판매 중인 상품만 조회 (상품 목록 페이지)
     * SELECT * FROM products
     * WHERE is_active = TRUE
     * ORDER BY created_at DESC;
     *
     * -- 비활성 상품 조회 (관리자 페이지)
     * SELECT * FROM products
     * WHERE is_active = FALSE;
     *
     * 【 복합 인덱스 활용 】
     * CREATE INDEX idx_products_active_category
     * ON products(is_active, category_id, created_at DESC);
     * → 카테고리별 활성 상품을 최신순 조회 (매우 빠름)
     */
    public bool IsActive { get; set; } = true;

    /**
     * 생성 일시 (상품 등록 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     * - INDEX: idx_products_created_at (시계열 조회)
     *
     * 【 실전 쿼리 】
     * -- 신상품 조회 (최근 7일)
     * SELECT * FROM products
     * WHERE is_active = TRUE
     *   AND created_at >= NOW() - INTERVAL '7 days'
     * ORDER BY created_at DESC;
     *
     * -- 월별 상품 등록 수
     * SELECT DATE_TRUNC('month', created_at) AS month,
     *        COUNT(*) AS new_products
     * FROM products
     * GROUP BY month
     * ORDER BY month DESC;
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (상품 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 가격 변경: price 업데이트 시 updated_at 갱신
     * - 재고 변경: stock_quantity 업데이트 시 updated_at 갱신
     *
     * 【 UPDATE 예시 】
     * UPDATE products
     * SET price = 89000,
     *     updated_at = NOW()
     * WHERE id = 12345;
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 상품 카테고리 (N:1 관계)
     *
     * 【 관계 】
     * - products (N) → categories (1)
     * - 여러 상품이 한 카테고리에 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT p.*, c.category_name
     * FROM products p
     * JOIN categories c ON p.category_id = c.id
     * WHERE p.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Product product = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"카테고리: {product.Category?.CategoryName}");
     */
    public Category? Category { get; set; }

    /**
     * 상품 이미지 목록 (1:N 관계)
     *
     * 【 관계 】
     * - products (1) ↔ product_images (N)
     * - 한 상품은 여러 이미지를 가질 수 있음 (대표, 상세 등)
     *
     * 【 SQL JOIN 예시 】
     * SELECT pi.*
     * FROM product_images pi
     * WHERE pi.product_id = 12345
     * ORDER BY pi.display_order ASC;
     *
     * 【 .NET 사용 예시 】
     * Product product = await repository.GetByIdAsync(12345);
     * foreach (var image in product.ProductImages ?? Enumerable.Empty<ProductImage>())
     * {
     *     Console.WriteLine($"이미지: {image.ImageUrl}");
     * }
     */
    public ICollection<ProductImage>? ProductImages { get; set; }

    /**
     * 주문 상품 목록 (1:N 관계)
     *
     * 【 관계 】
     * - products (1) ↔ order_items (N)
     * - 한 상품은 여러 주문에 포함될 수 있음
     *
     * 【 SQL 예시 】
     * -- 특정 상품의 총 판매량
     * SELECT SUM(quantity) AS total_sold
     * FROM order_items
     * WHERE product_id = 12345;
     */
    public ICollection<OrderItem>? OrderItems { get; set; }

    /**
     * 상품 리뷰 목록 (1:N 관계)
     *
     * 【 관계 】
     * - products (1) ↔ reviews (N)
     * - 한 상품은 여러 리뷰를 가질 수 있음
     *
     * 【 SQL 예시 】
     * -- 상품 평점 계산
     * SELECT AVG(rating) AS avg_rating,
     *        COUNT(*) AS review_count
     * FROM reviews
     * WHERE product_id = 12345;
     */
    public ICollection<Review>? Reviews { get; set; }

    /**
     * 재고 변동 이력 (1:N 관계)
     *
     * 【 관계 】
     * - products (1) ↔ inventory_logs (N)
     * - 한 상품은 여러 재고 변동 이력을 가짐
     *
     * 【 SQL 예시 】
     * -- 재고 변동 이력 조회
     * SELECT *
     * FROM inventory_logs
     * WHERE product_id = 12345
     * ORDER BY created_at DESC
     * LIMIT 10;
     */
    public ICollection<InventoryLog>? InventoryLogs { get; set; }

    /**
     * 찜 목록 (N:M 관계)
     *
     * 【 관계 】
     * - products (N) ↔ wishlists (M) ↔ users (N)
     * - 여러 사용자가 여러 상품을 찜할 수 있음
     * - 중간 테이블: wishlists (user_id, product_id)
     *
     * 【 SQL 예시 】
     * -- 특정 상품을 찜한 사용자 수
     * SELECT COUNT(*) AS wishlist_count
     * FROM wishlists
     * WHERE product_id = 12345;
     */
    public ICollection<Wishlist>? Wishlists { get; set; }
}
