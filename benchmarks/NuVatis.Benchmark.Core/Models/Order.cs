namespace NuVatis.Benchmark.Core.Models;

/**
 * 주문 엔티티 (Entity) - 데이터베이스의 orders 테이블과 1:1 매핑되는 C# 클래스
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - 전자상거래 시스템의 핵심 엔티티 (매출, 재고, 배송의 중심)
 *
 * 【 테이블 정보 】
 * - 테이블명: orders
 * - 레코드 수: 10,000,000개 (1천만 건, 3년치 주문 데이터)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key:
 *   - user_id → users.id (주문자)
 *   - coupon_id → coupons.id (사용한 쿠폰, 선택)
 *
 * 【 비즈니스 중요도 】
 * - 매출 분석: SUM(total_amount) GROUP BY date
 * - 고객 행동 분석: 주문 빈도, 평균 주문 금액
 * - 재고 관리: 주문 → 재고 차감
 * - 배송 관리: 주문 → 배송 생성
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Order
{
    /**
     * 주문 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가 (1, 2, 3, ...)
     *
     * 【 실전 활용 】
     * - 주문 조회: SELECT * FROM orders WHERE id = ?
     * - 주문 상세: orders → order_items JOIN
     */
    public long Id { get; set; }

    /**
     * 주문자 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: user_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (user_id) REFERENCES users(id)
     * - INDEX: idx_orders_user_id (주문 조회 최적화)
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_orders_user_created ON orders(user_id, created_at DESC);
     * → 특정 사용자의 최근 주문 조회 최적화
     *
     * 【 SQL 예시 】
     * -- 특정 사용자의 모든 주문 조회
     * SELECT * FROM orders
     * WHERE user_id = 12345
     * ORDER BY created_at DESC;
     *
     * -- 사용자별 총 주문 금액
     * SELECT user_id, SUM(total_amount) AS total_spent
     * FROM orders
     * GROUP BY user_id
     * ORDER BY total_spent DESC
     * LIMIT 10; -- Top 10 고객
     */
    public long UserId { get; set; }

    /**
     * 주문 번호 (Order Number) - 사용자에게 보여지는 번호
     *
     * 【 SQL 매핑 】
     * - 컬럼: order_number VARCHAR(50) UNIQUE NOT NULL
     *
     * 【 형식 예시 】
     * - "ORD-20260301-000001" (날짜 + 일련번호)
     * - "2026030112345" (연월일 + 순번)
     * - UUID: "a3c2e8b4-7f91-4d6a-b2c5-3e8f9a1b4c7d"
     *
     * 【 왜 ID와 별도로 주문번호? 】
     * - ID: 내부 시스템용 (순차 증가, DB PK)
     * - 주문번호: 고객용 (추적 가능, 고객센터 문의 시 사용)
     *
     * 【 보안 고려사항 】
     * [비권장] 순차 번호 노출:
     *   ORD-000001, ORD-000002, ... → 일일 주문량 추정 가능
     *
     * [권장] 난수 또는 UUID 사용:
     *   ORD-8A3F9B2E, ORD-7C2D5E1A → 추정 불가
     */
    public string OrderNumber { get; set; } = string.Empty;

    /**
     * 주문 상태 (Order Status)
     *
     * 【 SQL 매핑 】
     * - 컬럼: order_status VARCHAR(20) NOT NULL
     * - 제약: CHECK (order_status IN ('pending', 'processing', 'shipped', 'delivered', 'cancelled'))
     * - INDEX: idx_orders_status (상태별 조회 최적화)
     *
     * 【 상태 전이 (State Transition) 】
     * pending → processing → shipped → delivered
     *    ↓
     * cancelled (어느 단계에서든 취소 가능)
     *
     * 【 각 상태 설명 】
     * - pending: 결제 대기 (장바구니 → 주문 전환 직후)
     * - processing: 결제 완료, 상품 준비 중
     * - shipped: 배송 시작 (택배사에 인계)
     * - delivered: 배송 완료 (고객 수령)
     * - cancelled: 주문 취소 (환불 처리)
     *
     * 【 실전 쿼리 】
     * -- 배송 대기 주문 조회 (배송팀)
     * SELECT * FROM orders
     * WHERE order_status = 'processing'
     * ORDER BY created_at ASC;
     *
     * -- 오늘 취소된 주문 수 (고객센터)
     * SELECT COUNT(*) FROM orders
     * WHERE order_status = 'cancelled'
     *   AND DATE(updated_at) = CURRENT_DATE;
     *
     * 【 .NET Enum 대안 】
     * public enum OrderStatus {
     *     Pending,
     *     Processing,
     *     Shipped,
     *     Delivered,
     *     Cancelled
     * }
     * public OrderStatus Status { get; set; }
     * → 타입 안전성, IntelliSense 지원
     */
    public string OrderStatus { get; set; } = string.Empty;

    /**
     * 소계 (Subtotal) - 상품 금액 합계 (할인 전)
     *
     * 【 SQL 매핑 】
     * - 컬럼: subtotal DECIMAL(10, 2) NOT NULL
     * - DECIMAL(10, 2): 정수 8자리 + 소수 2자리 (최대 99,999,999.99)
     *
     * 【 .NET decimal 타입 】
     * - decimal: 128비트 고정 소수점 (재무 계산 전용)
     * - double: 부동 소수점 (과학 계산용, 금융 계산 부적합)
     *
     * 【 왜 decimal? 】
     * [비권장] double 사용:
     *   double price = 0.1;
     *   double total = price + price + price; // 0.30000000000000004 (오차 발생!)
     *
     * [권장] decimal 사용:
     *   decimal price = 0.1m;
     *   decimal total = price + price + price; // 0.30 (정확)
     *
     * 【 계산 공식 】
     * subtotal = SUM(order_items.price * order_items.quantity)
     *
     * 예시:
     * - 상품 A: 10,000원 × 2개 = 20,000원
     * - 상품 B: 5,000원 × 3개 = 15,000원
     * - subtotal = 35,000원
     */
    public decimal Subtotal { get; set; }

    /**
     * 할인 금액 (Discount Amount)
     *
     * 【 SQL 매핑 】
     * - 컬럼: discount_amount DECIMAL(10, 2) DEFAULT 0
     *
     * 【 할인 종류 】
     * - 쿠폰 할인: coupon_id 사용 시 적용
     * - 포인트 할인: 적립 포인트 사용
     * - 프로모션 할인: 특정 기간 할인 이벤트
     *
     * 【 계산 예시 】
     * - subtotal: 35,000원
     * - discount_amount: 5,000원 (쿠폰 할인)
     * - 실제 결제 금액: 35,000 - 5,000 = 30,000원 (+ 세금 + 배송비)
     */
    public decimal DiscountAmount { get; set; } = 0;

    /**
     * 세금 (Tax Amount)
     *
     * 【 SQL 매핑 】
     * - 컬럼: tax_amount DECIMAL(10, 2) DEFAULT 0
     *
     * 【 세금 계산 】
     * - 부가가치세 (VAT): 10% (한국)
     *   tax_amount = (subtotal - discount_amount) × 0.1
     *
     * - 미국 판매세 (Sales Tax): 주별 상이 (0-10%)
     *   캘리포니아: 7.25%, 뉴욕: 4%
     *
     * 【 계산 예시 】
     * - subtotal: 35,000원
     * - discount: 5,000원
     * - 과세 표준: 30,000원
     * - tax (10%): 3,000원
     */
    public decimal TaxAmount { get; set; } = 0;

    /**
     * 배송비 (Shipping Fee)
     *
     * 【 SQL 매핑 】
     * - 컬럼: shipping_fee DECIMAL(10, 2) DEFAULT 0
     *
     * 【 배송비 정책 】
     * - 무료 배송: subtotal >= 50,000원 → shipping_fee = 0
     * - 기본 배송비: subtotal < 50,000원 → shipping_fee = 3,000원
     * - 제주/도서산간: 추가 3,000원
     *
     * 【 비즈니스 로직 예시 】
     * decimal CalculateShippingFee(decimal subtotal, string region)
     * {
     *     if (subtotal >= 50000m) return 0m; // 무료 배송
     *     decimal baseFee = 3000m;
     *     if (region == "제주" || region == "도서산간") baseFee += 3000m;
     *     return baseFee;
     * }
     */
    public decimal ShippingFee { get; set; } = 0;

    /**
     * 총 결제 금액 (Total Amount) - 최종 결제 금액
     *
     * 【 SQL 매핑 】
     * - 컬럼: total_amount DECIMAL(10, 2) NOT NULL
     *
     * 【 계산 공식 】
     * total_amount = subtotal - discount_amount + tax_amount + shipping_fee
     *
     * 【 계산 예시 】
     * - subtotal: 35,000원
     * - discount: 5,000원
     * - tax: 3,000원 (10% VAT)
     * - shipping: 3,000원
     * - total_amount = 35,000 - 5,000 + 3,000 + 3,000 = 36,000원
     *
     * 【 데이터 무결성 】
     * [권장] DB 트리거 또는 계산 컬럼:
     * CREATE TRIGGER trg_orders_total BEFORE INSERT OR UPDATE ON orders
     * FOR EACH ROW
     * SET NEW.total_amount = NEW.subtotal - NEW.discount_amount + NEW.tax_amount + NEW.shipping_fee;
     *
     * 또는 애플리케이션 계층에서 검증:
     * decimal expected = subtotal - discount + tax + shipping;
     * if (Math.Abs(totalAmount - expected) > 0.01m)
     *     throw new InvalidOperationException("Total amount mismatch");
     */
    public decimal TotalAmount { get; set; }

    /**
     * 쿠폰 ID (Foreign Key, 선택 항목)
     *
     * 【 SQL 매핑 】
     * - 컬럼: coupon_id BIGINT NULL
     * - FK: FOREIGN KEY (coupon_id) REFERENCES coupons(id)
     *
     * 【 .NET Nullable 개념 】
     * - long?: Nullable<long> (값이 없을 수 있음)
     * - null: 쿠폰 미사용
     * - 값 있음: 쿠폰 사용
     *
     * 【 실전 활용 】
     * -- 쿠폰 사용 주문 조회
     * SELECT * FROM orders
     * WHERE coupon_id IS NOT NULL;
     *
     * -- 특정 쿠폰 사용 횟수
     * SELECT COUNT(*) FROM orders
     * WHERE coupon_id = 123;
     */
    public long? CouponId { get; set; }

    /**
     * 생성 일시 (주문 생성 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     * - INDEX: idx_orders_created_at (시계열 조회 최적화)
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_orders_user_created ON orders(user_id, created_at DESC);
     * → 특정 사용자의 최근 주문 빠르게 조회
     *
     * 【 실전 쿼리 】
     * -- 오늘 주문 조회
     * SELECT * FROM orders
     * WHERE DATE(created_at) = CURRENT_DATE;
     *
     * -- 월별 매출 분석
     * SELECT DATE_TRUNC('month', created_at) AS month,
     *        SUM(total_amount) AS revenue
     * FROM orders
     * WHERE created_at >= '2026-01-01'
     * GROUP BY month
     * ORDER BY month;
     *
     * -- 시간대별 주문 분포
     * SELECT EXTRACT(HOUR FROM created_at) AS hour,
     *        COUNT(*) AS order_count
     * FROM orders
     * WHERE DATE(created_at) = CURRENT_DATE
     * GROUP BY hour
     * ORDER BY hour;
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (주문 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 상태 변경 추적: pending → processing 시각 기록
     * - 배송 완료 시각: order_status = 'delivered' 시 updated_at 갱신
     *
     * 【 UPDATE 예시 】
     * UPDATE orders
     * SET order_status = 'shipped',
     *     updated_at = NOW()
     * WHERE id = 12345;
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 주문자 정보 (N:1 관계)
     *
     * 【 관계 】
     * - orders (N) → users (1)
     * - 한 사용자는 여러 주문을 할 수 있음
     *
     * 【 SQL JOIN 예시 】
     * SELECT o.*, u.user_name, u.email
     * FROM orders o
     * JOIN users u ON o.user_id = u.id
     * WHERE o.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Order order = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"주문자: {order.User?.UserName}");
     */
    public User? User { get; set; }

    /**
     * 사용한 쿠폰 정보 (N:1 관계, 선택)
     *
     * 【 관계 】
     * - orders (N) → coupons (1)
     * - 여러 주문이 같은 쿠폰을 사용할 수 있음
     *
     * 【 SQL JOIN 예시 】
     * SELECT o.*, c.coupon_code, c.discount_rate
     * FROM orders o
     * LEFT JOIN coupons c ON o.coupon_id = c.id
     * WHERE o.id = 12345;
     *
     * 【 LEFT JOIN 사용 이유 】
     * - coupon_id가 NULL일 수 있음 (쿠폰 미사용)
     * - INNER JOIN: 쿠폰 사용 주문만 조회
     * - LEFT JOIN: 모든 주문 조회 (쿠폰 미사용 포함)
     */
    public Coupon? Coupon { get; set; }

    /**
     * 주문 상품 목록 (1:N 관계)
     *
     * 【 관계 】
     * - orders (1) ↔ order_items (N)
     * - 한 주문은 여러 상품을 포함
     *
     * 【 SQL JOIN 예시 】
     * SELECT oi.*, p.product_name, p.price
     * FROM order_items oi
     * JOIN products p ON oi.product_id = p.id
     * WHERE oi.order_id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Order order = await repository.GetByIdAsync(12345);
     * foreach (var item in order.OrderItems ?? Enumerable.Empty<OrderItem>())
     * {
     *     Console.WriteLine($"{item.Product?.ProductName}: {item.Quantity}개");
     * }
     *
     * 【 Eager Loading 예시 】
     * var order = await context.Orders
     *     .Include(o => o.OrderItems)
     *         .ThenInclude(oi => oi.Product)
     *     .FirstOrDefaultAsync(o => o.Id == 12345);
     * → 1번의 쿼리로 Order + OrderItems + Products 모두 로드
     */
    public ICollection<OrderItem>? OrderItems { get; set; }

    /**
     * 결제 정보 (1:1 관계)
     *
     * 【 관계 】
     * - orders (1) ↔ payments (1)
     * - 한 주문은 하나의 결제 정보를 가짐
     *
     * 【 SQL JOIN 예시 】
     * SELECT o.*, p.payment_method, p.payment_status
     * FROM orders o
     * LEFT JOIN payments p ON o.id = p.order_id
     * WHERE o.id = 12345;
     */
    public Payment? Payment { get; set; }

    /**
     * 배송 정보 (1:1 관계)
     *
     * 【 관계 】
     * - orders (1) ↔ shipments (1)
     * - 한 주문은 하나의 배송 정보를 가짐
     *
     * 【 SQL JOIN 예시 】
     * SELECT o.*, s.tracking_number, s.carrier
     * FROM orders o
     * LEFT JOIN shipments s ON o.id = s.order_id
     * WHERE o.id = 12345;
     *
     * 【 실전 활용 】
     * -- 배송 중인 주문 조회
     * SELECT o.order_number, s.tracking_number, s.carrier
     * FROM orders o
     * JOIN shipments s ON o.id = s.order_id
     * WHERE o.order_status = 'shipped';
     */
    public Shipment? Shipment { get; set; }
}
