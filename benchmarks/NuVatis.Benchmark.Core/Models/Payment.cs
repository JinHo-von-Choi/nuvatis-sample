namespace NuVatis.Benchmark.Core.Models;

/**
 * 결제 엔티티 (Entity) - 주문 결제 정보 (1:1 with Order)
 *
 * 【 테이블 정보 】
 * - 테이블명: payments
 * - 레코드 수: 10,000,000개 (주문당 1개)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: order_id → orders.id (1:1 관계)
 *
 * 【 1:1 관계 】
 * - 한 주문은 하나의 결제 정보만 가짐
 * - order_id에 UNIQUE 제약 조건
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Payment
{
    /**
     * 결제 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     */
    public long Id { get; set; }

    /**
     * 주문 ID (Foreign Key, 1:1 관계)
     *
     * 【 SQL 매핑 】
     * - 컬럼: order_id BIGINT UNIQUE NOT NULL
     * - FK: FOREIGN KEY (order_id) REFERENCES orders(id)
     * - UNIQUE: 한 주문당 1개 결제만 허용
     *
     * 【 1:1 관계 보장 】
     * CREATE UNIQUE INDEX idx_payments_order_id ON payments(order_id);
     * → 동일 order_id로 2개 이상 결제 생성 불가
     */
    public long OrderId { get; set; }

    /**
     * 결제 수단 (Payment Method)
     *
     * 【 SQL 매핑 】
     * - 컬럼: payment_method VARCHAR(20) NOT NULL
     * - 제약: CHECK (payment_method IN ('card', 'bank_transfer', 'paypal', 'kakao_pay', 'naver_pay'))
     *
     * 【 결제 수단 종류 】
     * - 'card': 신용카드/체크카드
     * - 'bank_transfer': 계좌이체
     * - 'paypal': PayPal
     * - 'kakao_pay': 카카오페이
     * - 'naver_pay': 네이버페이
     *
     * 【 SQL 예시 】
     * -- 결제 수단별 매출 통계
     * SELECT payment_method,
     *        COUNT(*) AS payment_count,
     *        SUM(amount) AS total_amount
     * FROM payments
     * WHERE payment_status = 'completed'
     * GROUP BY payment_method
     * ORDER BY total_amount DESC;
     */
    public string PaymentMethod { get; set; } = string.Empty;

    /**
     * 결제 상태 (Payment Status)
     *
     * 【 SQL 매핑 】
     * - 컬럼: payment_status VARCHAR(20) NOT NULL
     * - 제약: CHECK (payment_status IN ('pending', 'completed', 'failed', 'refunded'))
     *
     * 【 상태 전이 (State Transition) 】
     * pending → completed (정상 결제)
     * pending → failed (결제 실패)
     * completed → refunded (환불)
     *
     * 【 각 상태 설명 】
     * - pending: 결제 대기 (PG사 승인 대기)
     * - completed: 결제 완료
     * - failed: 결제 실패 (카드 한도 초과, 잔액 부족 등)
     * - refunded: 환불 완료
     *
     * 【 실전 쿼리 】
     * -- 결제 완료 건수 (일별)
     * SELECT DATE(paid_at) AS date,
     *        COUNT(*) AS completed_payments,
     *        SUM(amount) AS total_revenue
     * FROM payments
     * WHERE payment_status = 'completed'
     *   AND paid_at >= '2026-03-01'
     * GROUP BY date
     * ORDER BY date DESC;
     *
     * -- 결제 실패율
     * SELECT
     *   COUNT(CASE WHEN payment_status = 'failed' THEN 1 END) AS failed_count,
     *   COUNT(*) AS total_count,
     *   ROUND(COUNT(CASE WHEN payment_status = 'failed' THEN 1 END) * 100.0 / COUNT(*), 2) AS failure_rate
     * FROM payments;
     */
    public string PaymentStatus { get; set; } = string.Empty;

    /**
     * 결제 트랜잭션 ID (Transaction ID) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: transaction_id VARCHAR(100) NULL
     * - INDEX: idx_payments_transaction_id (빠른 조회)
     *
     * 【 실전 활용 】
     * - PG사 고유 번호: "IMP20260301-000001"
     * - 결제 취소/환불 시 필수
     * - 고객 문의 시 결제 추적
     *
     * 【 PG사 연동 예시 】
     * // 아임포트 (Iamport) 결제
     * var response = await IamportClient.PayAsync(new PaymentRequest {
     *     Amount = 100000,
     *     OrderId = "ORD-12345",
     *     ...
     * });
     *
     * payment.TransactionId = response.TransactionId; // "IMP20260301-000001"
     * payment.PaymentStatus = response.Status; // "completed"
     */
    public string? TransactionId { get; set; }

    /**
     * 결제 금액 (Amount)
     *
     * 【 SQL 매핑 】
     * - 컬럼: amount DECIMAL(10, 2) NOT NULL
     *
     * 【 데이터 무결성 】
     * - payment.amount == order.total_amount (일치해야 함)
     *
     * 【 검증 로직 】
     * if (Math.Abs(payment.Amount - order.TotalAmount) > 0.01m)
     *     throw new InvalidOperationException("결제 금액 불일치");
     *
     * 【 SQL 예시 】
     * -- 결제/주문 금액 불일치 검증
     * SELECT p.id, p.amount AS payment_amount,
     *        o.total_amount AS order_amount
     * FROM payments p
     * JOIN orders o ON p.order_id = o.id
     * WHERE ABS(p.amount - o.total_amount) > 0.01;
     */
    public decimal Amount { get; set; }

    /**
     * 결제 완료 일시 (Paid At) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: paid_at TIMESTAMP NULL
     *
     * 【 .NET Nullable 개념 】
     * - DateTime?: Nullable<DateTime>
     * - null: 결제 미완료 (pending, failed)
     * - 값 있음: 결제 완료 시각 (completed)
     *
     * 【 업데이트 예시 】
     * -- 결제 완료 시
     * UPDATE payments
     * SET payment_status = 'completed',
     *     paid_at = NOW(),
     *     updated_at = NOW()
     * WHERE id = 12345;
     *
     * 【 실전 활용 】
     * -- 결제 소요 시간 분석
     * SELECT AVG(EXTRACT(EPOCH FROM (paid_at - created_at))) AS avg_seconds
     * FROM payments
     * WHERE payment_status = 'completed';
     *
     * 결과: 평균 15초 (주문 생성 → 결제 완료)
     */
    public DateTime? PaidAt { get; set; }

    /**
     * 생성 일시 (결제 시도 시작 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 주문 생성 시각과 동일
     * - 결제 타임아웃 체크: NOW() - created_at > 30분
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (결제 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 상태 변경: pending → completed, failed, refunded
     * - 환불 처리 시각 기록
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 주문 정보 (1:1 관계)
     *
     * 【 관계 】
     * - payments (1) ↔ orders (1)
     * - 한 결제는 하나의 주문에 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT p.*, o.order_number, o.total_amount
     * FROM payments p
     * JOIN orders o ON p.order_id = o.id
     * WHERE p.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Payment payment = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"주문번호: {payment.Order?.OrderNumber}");
     * Console.WriteLine($"결제 금액: {payment.Amount:C}");
     * Console.WriteLine($"결제 상태: {payment.PaymentStatus}");
     */
    public Order? Order { get; set; }
}
