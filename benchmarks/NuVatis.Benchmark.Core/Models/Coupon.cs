namespace NuVatis.Benchmark.Core.Models;

/**
 * 쿠폰 엔티티 (Entity) - 할인 쿠폰 마스터 데이터
 *
 * 【 테이블 정보 】
 * - 테이블명: coupons
 * - 레코드 수: 1,000개 (쿠폰 종류)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 *
 * 【 비즈니스 활용 】
 * - 프로모션: 신규 회원 가입 쿠폰, 생일 쿠폰
 * - 마케팅: 이메일 발송, 앱 푸시 알림
 * - 재구매 유도: 1개월 미사용 고객 대상 쿠폰
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Coupon
{
    /**
     * 쿠폰 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     */
    public long Id { get; set; }

    /**
     * 쿠폰 코드 (Coupon Code)
     *
     * 【 SQL 매핑 】
     * - 컬럼: coupon_code VARCHAR(50) UNIQUE NOT NULL
     * - UNIQUE: 중복 방지
     *
     * 【 형식 예시 】
     * - "WELCOME2026" (신규 가입)
     * - "BIRTHDAY20" (생일 쿠폰)
     * - "SAVE10K" (1만원 할인)
     *
     * 【 생성 규칙 】
     * - 대문자 + 숫자 조합
     * - 최소 6자 이상
     * - 의미 있는 이름 (WELCOME, SAVE, BIRTHDAY)
     */
    public string CouponCode { get; set; } = string.Empty;

    /**
     * 할인 타입 (Discount Type)
     *
     * 【 SQL 매핑 】
     * - 컬럼: discount_type VARCHAR(20) NOT NULL
     * - 제약: CHECK (discount_type IN ('percentage', 'fixed'))
     *
     * 【 할인 종류 】
     * - 'percentage': 퍼센트 할인 (10%, 20%)
     * - 'fixed': 고정 금액 할인 (5,000원, 10,000원)
     *
     * 【 계산 예시 】
     * [percentage] discount_value = 10 (10% 할인)
     *   - 주문 금액: 100,000원
     *   - 할인 금액: 100,000 × 0.1 = 10,000원
     *   - 최종 금액: 90,000원
     *
     * [fixed] discount_value = 10000 (10,000원 할인)
     *   - 주문 금액: 100,000원
     *   - 할인 금액: 10,000원
     *   - 최종 금액: 90,000원
     */
    public string DiscountType { get; set; } = string.Empty;

    /**
     * 할인 값 (Discount Value)
     *
     * 【 SQL 매핑 】
     * - 컬럼: discount_value DECIMAL(10, 2) NOT NULL
     *
     * 【 의미 】
     * - discount_type = 'percentage': 퍼센트 (10 = 10%)
     * - discount_type = 'fixed': 금액 (10000 = 10,000원)
     */
    public decimal DiscountValue { get; set; }

    /**
     * 최소 주문 금액 (Min Order Amount) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: min_order_amount DECIMAL(10, 2) NULL
     *
     * 【 실전 활용 】
     * - "50,000원 이상 구매 시 10% 할인"
     * - min_order_amount = 50000
     *
     * 【 검증 로직 】
     * if (minOrderAmount.HasValue && orderAmount < minOrderAmount.Value)
     *     throw new InvalidOperationException("최소 주문 금액 미달");
     */
    public decimal? MinOrderAmount { get; set; }

    /**
     * 최대 할인 금액 (Max Discount) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: max_discount DECIMAL(10, 2) NULL
     *
     * 【 실전 활용 】
     * - "20% 할인, 최대 50,000원"
     * - discount_type = 'percentage', discount_value = 20
     * - max_discount = 50000
     *
     * 【 계산 예시 】
     * - 주문 금액: 1,000,000원
     * - 20% 할인: 200,000원
     * - 최대 할인: 50,000원
     * - 실제 할인: MIN(200,000, 50,000) = 50,000원
     *
     * 【 계산 로직 】
     * decimal discount = discountType == "percentage"
     *     ? orderAmount * (discountValue / 100m)
     *     : discountValue;
     *
     * if (maxDiscount.HasValue && discount > maxDiscount.Value)
     *     discount = maxDiscount.Value;
     */
    public decimal? MaxDiscount { get; set; }

    /**
     * 유효 시작 일시 (Valid From)
     *
     * 【 SQL 매핑 】
     * - 컬럼: valid_from TIMESTAMP NOT NULL
     *
     * 【 실전 활용 】
     * - 이벤트 시작: 2026-03-01 00:00:00
     * - 검증: NOW() >= valid_from
     */
    public DateTime ValidFrom { get; set; }

    /**
     * 유효 종료 일시 (Valid Until)
     *
     * 【 SQL 매핑 】
     * - 컬럼: valid_until TIMESTAMP NOT NULL
     *
     * 【 실전 활용 】
     * - 이벤트 종료: 2026-03-31 23:59:59
     * - 검증: NOW() <= valid_until
     *
     * 【 유효성 검증 SQL 】
     * SELECT * FROM coupons
     * WHERE coupon_code = 'WELCOME2026'
     *   AND NOW() BETWEEN valid_from AND valid_until
     *   AND is_active = TRUE;
     */
    public DateTime ValidUntil { get; set; }

    /**
     * 사용 제한 횟수 (Usage Limit) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: usage_limit INT NULL
     *
     * 【 실전 활용 】
     * - null: 무제한 사용
     * - 100: 선착순 100명
     * - 1: 1인 1회 사용 (user_coupons 테이블로 관리)
     *
     * 【 검증 로직 】
     * if (usageLimit.HasValue && usageCount >= usageLimit.Value)
     *     throw new InvalidOperationException("쿠폰 사용 한도 초과");
     */
    public int? UsageLimit { get; set; }

    /**
     * 사용 횟수 (Usage Count)
     *
     * 【 SQL 매핑 】
     * - 컬럼: usage_count INT DEFAULT 0
     *
     * 【 업데이트 로직 】
     * -- 주문 생성 시 사용 횟수 증가
     * UPDATE coupons
     * SET usage_count = usage_count + 1
     * WHERE id = 123
     *   AND (usage_limit IS NULL OR usage_count < usage_limit);
     *
     * IF (ROW_COUNT() = 0) THEN
     *     RAISE EXCEPTION '쿠폰 사용 불가';
     * END IF;
     *
     * 【 동시성 제어 】
     * - Atomic Update: usage_count 증가는 원자적 연산
     * - Race Condition 방지
     */
    public int UsageCount { get; set; } = 0;

    /**
     * 활성 상태 (Is Active)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_active BOOLEAN DEFAULT TRUE
     *
     * 【 실전 활용 】
     * - 쿠폰 중단: is_active = FALSE (긴급 중지)
     * - 재활성화: is_active = TRUE
     */
    public bool IsActive { get; set; } = true;

    /**
     * 생성 일시 (쿠폰 생성 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     */
    public DateTime CreatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 쿠폰 사용 주문 목록 (1:N 관계)
     *
     * 【 관계 】
     * - coupons (1) ↔ orders (N)
     * - 한 쿠폰은 여러 주문에 사용될 수 있음
     */
    public ICollection<Order>? Orders { get; set; }

    /**
     * 사용자별 쿠폰 발급 내역 (1:N 관계)
     *
     * 【 관계 】
     * - coupons (1) ↔ user_coupons (N)
     * - 한 쿠폰은 여러 사용자에게 발급될 수 있음
     */
    public ICollection<UserCoupon>? UserCoupons { get; set; }
}
