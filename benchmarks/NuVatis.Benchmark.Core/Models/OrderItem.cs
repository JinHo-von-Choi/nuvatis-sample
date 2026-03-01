namespace NuVatis.Benchmark.Core.Models;

/**
 * 주문 상세 항목 엔티티 (Entity) - 주문에 포함된 개별 상품
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - Order와 Product의 다대다(N:M) 관계를 풀어주는 중간 엔티티
 *
 * 【 테이블 정보 】
 * - 테이블명: order_items
 * - 레코드 수: 50,000,000개 (5천만 건, 주문당 평균 5개 상품)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key:
 *   - order_id → orders.id (주문)
 *   - product_id → products.id (상품)
 * - 복합 인덱스: (order_id, product_id) - 빠른 조회
 *
 * 【 왜 중간 테이블? 】
 * [잘못된 설계] Order에 Product를 직접 저장:
 *   orders 테이블에 product_id 컬럼 추가?
 *   → 한 주문에 1개 상품만 가능 (비현실적)
 *
 * [올바른 설계] OrderItem 중간 테이블:
 *   - 한 주문(Order)은 여러 상품(OrderItem) 포함 가능
 *   - 각 OrderItem은 수량, 가격, 할인 정보 저장
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class OrderItem
{
    /**
     * 주문 상품 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가
     */
    public long Id { get; set; }

    /**
     * 주문 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: order_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (order_id) REFERENCES orders(id)
     * - INDEX: idx_order_items_order_id (주문별 상품 조회 최적화)
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_order_items_order_product
     * ON order_items(order_id, product_id);
     * → 특정 주문의 특정 상품 빠르게 조회
     *
     * 【 SQL 예시 】
     * -- 특정 주문의 모든 상품 조회
     * SELECT oi.*, p.product_name, p.price
     * FROM order_items oi
     * JOIN products p ON oi.product_id = p.id
     * WHERE oi.order_id = 12345;
     *
     * -- 주문별 상품 수
     * SELECT order_id, COUNT(*) AS item_count
     * FROM order_items
     * WHERE order_id IN (12345, 12346, 12347)
     * GROUP BY order_id;
     */
    public long OrderId { get; set; }

    /**
     * 상품 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: product_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (product_id) REFERENCES products(id)
     * - INDEX: idx_order_items_product_id (상품별 판매 현황 조회)
     *
     * 【 SQL 예시 】
     * -- 특정 상품의 총 판매량
     * SELECT product_id,
     *        SUM(quantity) AS total_sold,
     *        SUM(total_price) AS total_revenue
     * FROM order_items
     * WHERE product_id = 123
     * GROUP BY product_id;
     *
     * -- 베스트셀러 TOP 10
     * SELECT p.product_name,
     *        SUM(oi.quantity) AS total_sold
     * FROM order_items oi
     * JOIN products p ON oi.product_id = p.id
     * GROUP BY p.id, p.product_name
     * ORDER BY total_sold DESC
     * LIMIT 10;
     *
     * 【 재고 차감 로직 】
     * -- 주문 생성 시 재고 감소
     * BEGIN;
     * INSERT INTO order_items (order_id, product_id, quantity, ...)
     * VALUES (12345, 123, 2, ...);
     *
     * UPDATE products
     * SET stock_quantity = stock_quantity - 2
     * WHERE id = 123
     *   AND stock_quantity >= 2; -- 재고 부족 시 실패
     *
     * IF (ROW_COUNT() = 0) THEN
     *     ROLLBACK;
     *     RAISE EXCEPTION '재고 부족';
     * END IF;
     * COMMIT;
     */
    public long ProductId { get; set; }

    /**
     * 주문 수량 (Quantity)
     *
     * 【 SQL 매핑 】
     * - 컬럼: quantity INT NOT NULL
     * - 제약: CHECK (quantity > 0) - 0개 이하 주문 방지
     *
     * 【 .NET int 타입 】
     * - int: 32비트 정수형 (-21억 ~ +21억)
     * - 주문 수량: 1 이상 (음수 불가)
     *
     * 【 실전 활용 】
     * -- 대량 주문 조회 (B2B 고객)
     * SELECT * FROM order_items
     * WHERE quantity >= 100
     * ORDER BY quantity DESC;
     *
     * -- 평균 주문 수량
     * SELECT AVG(quantity) AS avg_quantity
     * FROM order_items;
     *
     * 【 비즈니스 규칙 】
     * - 최소 주문 수량: quantity >= 1
     * - 최대 주문 수량: quantity <= stock_quantity (재고 확인)
     * - 대량 구매 할인: quantity >= 10 → 10% 할인
     */
    public int Quantity { get; set; }

    /**
     * 단가 (Unit Price) - 주문 시점의 상품 가격
     *
     * 【 SQL 매핑 】
     * - 컬럼: unit_price DECIMAL(10, 2) NOT NULL
     *
     * 【 왜 단가를 저장? 】
     * [잘못된 설계] 상품 가격을 실시간 조회:
     *   SELECT p.price FROM products WHERE id = 123;
     *   → 상품 가격 변경 시 과거 주문의 금액도 변경됨 (잘못!)
     *
     * [올바른 설계] 주문 시점의 가격 저장:
     *   unit_price = products.price (주문 생성 시)
     *   → 상품 가격이 변경되어도 과거 주문 금액은 유지
     *
     * 【 실전 예시 】
     * - 주문 시점 (2026-01-01): 노트북 가격 1,000,000원 → unit_price = 1,000,000
     * - 가격 인상 (2026-02-01): 노트북 가격 1,200,000원 → products.price = 1,200,000
     * - 과거 주문 조회 (2026-03-01): unit_price = 1,000,000 (변경 없음, 정확)
     *
     * 【 감사 추적 (Audit Trail) 】
     * - 주문 시점의 정확한 금액 보존
     * - 환불, 교환 시 원래 금액 기준
     * - 회계 감사 시 필수
     */
    public decimal UnitPrice { get; set; }

    /**
     * 할인 금액 (Discount Amount) - 개별 상품 할인
     *
     * 【 SQL 매핑 】
     * - 컬럼: discount_amount DECIMAL(10, 2) DEFAULT 0
     *
     * 【 할인 종류 】
     * - 상품 할인: 특정 상품 20% 할인
     * - 수량 할인: 10개 이상 구매 시 10% 할인
     * - 프로모션: 2+1 행사 (3개 가격으로 2개만 계산)
     *
     * 【 계산 예시 】
     * - unit_price: 100,000원
     * - quantity: 3개
     * - discount_amount: 30,000원 (10% 할인)
     * - total_price: (100,000 × 3) - 30,000 = 270,000원
     *
     * 【 주의 】
     * - Order 레벨 할인 (쿠폰): order.discount_amount
     * - OrderItem 레벨 할인 (상품): order_item.discount_amount
     * - 총 할인 = SUM(order_items.discount_amount) + order.discount_amount
     */
    public decimal DiscountAmount { get; set; } = 0;

    /**
     * 총 가격 (Total Price) - 해당 상품의 최종 금액
     *
     * 【 SQL 매핑 】
     * - 컬럼: total_price DECIMAL(10, 2) NOT NULL
     *
     * 【 계산 공식 】
     * total_price = (unit_price × quantity) - discount_amount
     *
     * 【 계산 예시 】
     * - unit_price: 100,000원
     * - quantity: 2개
     * - discount_amount: 10,000원
     * - total_price = (100,000 × 2) - 10,000 = 190,000원
     *
     * 【 데이터 무결성 검증 】
     * decimal expected = (unitPrice * quantity) - discountAmount;
     * if (Math.Abs(totalPrice - expected) > 0.01m)
     *     throw new InvalidOperationException("Total price mismatch");
     *
     * 【 주문 총액 계산 】
     * -- 주문의 총 상품 금액
     * SELECT order_id, SUM(total_price) AS subtotal
     * FROM order_items
     * WHERE order_id = 12345
     * GROUP BY order_id;
     *
     * -- 전체 주문 금액 (상품 + 세금 + 배송비)
     * order.total_amount = SUM(order_items.total_price)
     *                    + order.tax_amount
     *                    + order.shipping_fee
     */
    public decimal TotalPrice { get; set; }

    /**
     * 생성 일시 (주문 상품 추가 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 주문 생성 시각과 동일
     * - 분할 배송 시 일부 상품만 먼저 추가 가능
     */
    public DateTime CreatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 주문 정보 (N:1 관계)
     *
     * 【 관계 】
     * - order_items (N) → orders (1)
     * - 여러 주문 상품이 한 주문에 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT oi.*, o.order_number, o.order_status
     * FROM order_items oi
     * JOIN orders o ON oi.order_id = o.id
     * WHERE oi.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * OrderItem item = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"주문번호: {item.Order?.OrderNumber}");
     */
    public Order? Order { get; set; }

    /**
     * 상품 정보 (N:1 관계)
     *
     * 【 관계 】
     * - order_items (N) → products (1)
     * - 여러 주문 상품이 같은 상품을 참조할 수 있음
     *
     * 【 SQL JOIN 예시 】
     * SELECT oi.*, p.product_name, p.sku
     * FROM order_items oi
     * JOIN products p ON oi.product_id = p.id
     * WHERE oi.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * OrderItem item = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"상품명: {item.Product?.ProductName}");
     * Console.WriteLine($"수량: {item.Quantity}개");
     * Console.WriteLine($"금액: {item.TotalPrice:C}"); // 통화 형식 (₩190,000)
     *
     * 【 주의사항 】
     * - Product 정보는 최신 정보 (가격 변경 가능)
     * - 주문 시점 가격: item.UnitPrice (변경 없음)
     * - 현재 가격: item.Product.Price (변경될 수 있음)
     */
    public Product? Product { get; set; }
}
