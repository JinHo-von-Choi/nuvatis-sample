namespace NuVatis.Benchmark.Core.Models;

/**
 * 사용자-쿠폰 관계 엔티티 (Entity) - N:M 중간 테이블
 *
 * 【 테이블 정보 】
 * - 테이블명: user_coupons
 * - 레코드 수: 500,000개 (사용자별 발급된 쿠폰)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key:
 *   - user_id → users.id
 *   - coupon_id → coupons.id
 * - 복합 UNIQUE: (user_id, coupon_id) - 중복 발급 방지
 *
 * 【 N:M 관계 】
 * - users (N) ↔ user_coupons (중간 테이블) ↔ coupons (M)
 * - 한 사용자는 여러 쿠폰을 가질 수 있음
 * - 한 쿠폰은 여러 사용자에게 발급될 수 있음
 *
 * 【 왜 중간 테이블? 】
 * - 사용자별 쿠폰 발급 이력 관리
 * - 쿠폰 사용 여부 추적 (is_used, used_at)
 * - 1인 1회 사용 제한 구현
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class UserCoupon
{
    /**
     * 사용자-쿠폰 관계 고유 식별자 (Primary Key)
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
     * - INDEX: idx_user_coupons_user_id (사용자별 쿠폰 조회)
     *
     * 【 복합 UNIQUE 제약 】
     * CREATE UNIQUE INDEX idx_user_coupons_user_coupon
     * ON user_coupons(user_id, coupon_id);
     * → 동일 사용자가 동일 쿠폰을 2번 이상 발급받지 못하도록 방지
     *
     * 【 SQL 예시 】
     * -- 사용자의 사용 가능한 쿠폰 조회
     * SELECT uc.*, c.coupon_code, c.discount_type, c.discount_value
     * FROM user_coupons uc
     * JOIN coupons c ON uc.coupon_id = c.id
     * WHERE uc.user_id = 12345
     *   AND uc.is_used = FALSE
     *   AND c.is_active = TRUE
     *   AND NOW() BETWEEN c.valid_from AND c.valid_until;
     */
    public long UserId { get; set; }

    /**
     * 쿠폰 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: coupon_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (coupon_id) REFERENCES coupons(id)
     * - INDEX: idx_user_coupons_coupon_id (쿠폰별 발급 현황 조회)
     *
     * 【 SQL 예시 】
     * -- 특정 쿠폰의 발급 및 사용 현황
     * SELECT
     *   COUNT(*) AS issued_count,
     *   COUNT(CASE WHEN is_used THEN 1 END) AS used_count,
     *   COUNT(CASE WHEN NOT is_used THEN 1 END) AS unused_count
     * FROM user_coupons
     * WHERE coupon_id = 123;
     *
     * 결과:
     * | issued_count | used_count | unused_count |
     * |--------------|------------|--------------|
     * | 1,000        | 650        | 350          |
     *
     * 사용률 = 650 / 1,000 = 65%
     */
    public long CouponId { get; set; }

    /**
     * 사용 여부 (Is Used)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_used BOOLEAN DEFAULT FALSE
     *
     * 【 상태 전이 】
     * - FALSE: 미사용 (발급 후 아직 사용 안 함)
     * - TRUE: 사용 완료 (주문에 적용됨)
     *
     * 【 업데이트 로직 】
     * -- 주문 생성 시 쿠폰 사용 처리
     * BEGIN;
     * UPDATE user_coupons
     * SET is_used = TRUE,
     *     used_at = NOW()
     * WHERE user_id = 12345
     *   AND coupon_id = 123
     *   AND is_used = FALSE; -- 이미 사용된 쿠폰 재사용 방지
     *
     * IF (ROW_COUNT() = 0) THEN
     *     ROLLBACK;
     *     RAISE EXCEPTION '쿠폰을 사용할 수 없습니다';
     * END IF;
     *
     * INSERT INTO orders (user_id, coupon_id, ...) VALUES (...);
     * COMMIT;
     *
     * 【 실전 쿼리 】
     * -- 미사용 쿠폰 조회 (마이페이지)
     * SELECT * FROM user_coupons
     * WHERE user_id = 12345
     *   AND is_used = FALSE
     * ORDER BY created_at DESC;
     */
    public bool IsUsed { get; set; } = false;

    /**
     * 사용 일시 (Used At) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: used_at TIMESTAMP NULL
     *
     * 【 .NET Nullable 개념 】
     * - DateTime?: Nullable<DateTime>
     * - null: 미사용 (is_used = FALSE)
     * - 값 있음: 사용 완료 시각 (is_used = TRUE)
     *
     * 【 데이터 무결성 검증 】
     * if (isUsed && !usedAt.HasValue)
     *     throw new InvalidOperationException("사용 일시 누락");
     *
     * if (!isUsed && usedAt.HasValue)
     *     throw new InvalidOperationException("데이터 불일치");
     *
     * 【 SQL 검증 쿼리 】
     * -- 데이터 무결성 오류 검출
     * SELECT * FROM user_coupons
     * WHERE (is_used = TRUE AND used_at IS NULL)
     *    OR (is_used = FALSE AND used_at IS NOT NULL);
     *
     * → 결과가 없어야 정상
     *
     * 【 실전 활용 】
     * -- 쿠폰 발급 후 사용까지 걸린 시간 분석
     * SELECT AVG(EXTRACT(EPOCH FROM (used_at - created_at)) / 86400) AS avg_days
     * FROM user_coupons
     * WHERE is_used = TRUE;
     *
     * 결과: 평균 7일 (쿠폰 발급 후 7일 만에 사용)
     */
    public DateTime? UsedAt { get; set; }

    /**
     * 생성 일시 (쿠폰 발급 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 쿠폰 발급 이벤트: 신규 가입, 생일, 프로모션
     * - 만료 확인: created_at + 30일 < NOW() (발급 후 30일 경과)
     *
     * 【 SQL 예시 】
     * -- 최근 7일 내 발급된 쿠폰 (신규)
     * SELECT * FROM user_coupons
     * WHERE created_at >= NOW() - INTERVAL '7 days'
     * ORDER BY created_at DESC;
     *
     * -- 발급 후 미사용 쿠폰 (알림 대상)
     * SELECT uc.*, u.email, c.coupon_code
     * FROM user_coupons uc
     * JOIN users u ON uc.user_id = u.id
     * JOIN coupons c ON uc.coupon_id = c.id
     * WHERE uc.is_used = FALSE
     *   AND uc.created_at < NOW() - INTERVAL '7 days'
     *   AND uc.created_at > NOW() - INTERVAL '30 days';
     *
     * → 발급 후 7~30일 경과한 미사용 쿠폰 (리마인드 이메일 발송)
     */
    public DateTime CreatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 사용자 정보 (N:1 관계)
     *
     * 【 관계 】
     * - user_coupons (N) → users (1)
     * - 여러 쿠폰 발급 내역이 한 사용자에게 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT uc.*, u.user_name, u.email
     * FROM user_coupons uc
     * JOIN users u ON uc.user_id = u.id
     * WHERE uc.id = 12345;
     */
    public User? User { get; set; }

    /**
     * 쿠폰 정보 (N:1 관계)
     *
     * 【 관계 】
     * - user_coupons (N) → coupons (1)
     * - 여러 사용자 발급 내역이 한 쿠폰을 참조
     *
     * 【 SQL JOIN 예시 】
     * SELECT uc.*, c.coupon_code, c.discount_type, c.discount_value
     * FROM user_coupons uc
     * JOIN coupons c ON uc.coupon_id = c.id
     * WHERE uc.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * UserCoupon userCoupon = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"사용자: {userCoupon.User?.UserName}");
     * Console.WriteLine($"쿠폰 코드: {userCoupon.Coupon?.CouponCode}");
     * Console.WriteLine($"할인: {userCoupon.Coupon?.DiscountValue}{(userCoupon.Coupon?.DiscountType == "percentage" ? "%" : "원")}");
     * Console.WriteLine($"사용 여부: {(userCoupon.IsUsed ? "사용 완료" : "미사용")}");
     *
     * if (userCoupon.UsedAt.HasValue)
     * {
     *     Console.WriteLine($"사용 일시: {userCoupon.UsedAt:yyyy-MM-dd HH:mm}");
     * }
     */
    public Coupon? Coupon { get; set; }
}
