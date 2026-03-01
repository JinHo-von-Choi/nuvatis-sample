namespace NuVatis.Benchmark.Core.Models;

/**
 * 재고 변동 이력 엔티티 (Entity) - 상품 재고 변동 추적
 *
 * 【 테이블 정보 】
 * - 테이블명: inventory_logs
 * - 레코드 수: 20,000,000개 (2천만 건, 재고 변동 이력)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: product_id → products.id
 *
 * 【 왜 재고 이력이 필요한가? 】
 * - 감사 추적 (Audit Trail): 재고 변동 원인 파악
 * - 재고 회계: 재고 자산 가치 계산
 * - 분쟁 해결: 재고 오차 발생 시 원인 조사
 * - 수요 예측: 판매 패턴 분석
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class InventoryLog
{
    /**
     * 재고 이력 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     */
    public long Id { get; set; }

    /**
     * 상품 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: product_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (product_id) REFERENCES products(id)
     * - INDEX: idx_inventory_logs_product_created (상품별 이력 조회)
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_inventory_logs_product_created
     * ON inventory_logs(product_id, created_at DESC);
     * → 상품별 최신 이력 빠르게 조회
     */
    public long ProductId { get; set; }

    /**
     * 변동 유형 (Change Type)
     *
     * 【 SQL 매핑 】
     * - 컬럼: change_type VARCHAR(20) NOT NULL
     * - 제약: CHECK (change_type IN ('restock', 'sale', 'return', 'adjustment'))
     *
     * 【 변동 유형 설명 】
     * - 'restock': 입고 (재고 증가)
     *   예: 공장에서 상품 100개 입고 → +100
     *
     * - 'sale': 판매 (재고 감소)
     *   예: 고객이 상품 2개 구매 → -2
     *
     * - 'return': 반품 (재고 증가)
     *   예: 고객이 상품 1개 반품 → +1
     *
     * - 'adjustment': 재고 조정 (증가 또는 감소)
     *   예: 실사 결과 재고 차이 발견 → +5 또는 -5
     *   파손, 분실, 도난 등
     *
     * 【 SQL 예시 】
     * -- 유형별 재고 변동 통계
     * SELECT change_type,
     *        COUNT(*) AS count,
     *        SUM(quantity_change) AS total_change
     * FROM inventory_logs
     * WHERE product_id = 123
     * GROUP BY change_type
     * ORDER BY count DESC;
     *
     * 결과:
     * | change_type | count | total_change |
     * |-------------|-------|--------------|
     * | sale        | 500   | -1,000       |
     * | restock     | 10    | +2,000       |
     * | return      | 20    | +25          |
     * | adjustment  | 2     | -5           |
     */
    public string ChangeType { get; set; } = string.Empty;

    /**
     * 변동 수량 (Quantity Change)
     *
     * 【 SQL 매핑 】
     * - 컬럼: quantity_change INT NOT NULL
     *
     * 【 .NET int 타입 】
     * - int: 32비트 정수형
     * - 양수: 재고 증가 (+100, +2)
     * - 음수: 재고 감소 (-2, -50)
     *
     * 【 실전 예시 】
     * - 입고: quantity_change = +100 (100개 추가)
     * - 판매: quantity_change = -2 (2개 차감)
     * - 반품: quantity_change = +1 (1개 복구)
     * - 조정: quantity_change = -5 (5개 감소, 파손)
     *
     * 【 SQL 예시 】
     * -- 일별 재고 변동량
     * SELECT DATE(created_at) AS date,
     *        SUM(quantity_change) AS net_change
     * FROM inventory_logs
     * WHERE product_id = 123
     * GROUP BY date
     * ORDER BY date DESC;
     */
    public int QuantityChange { get; set; }

    /**
     * 변동 전 수량 (Quantity Before)
     *
     * 【 SQL 매핑 】
     * - 컬럼: quantity_before INT NOT NULL
     *
     * 【 실전 활용 】
     * - 변동 전 재고: 100개
     * - 판매: 2개
     * - quantity_before = 100
     * - quantity_after = 98
     *
     * 【 검증 로직 】
     * if (quantityAfter != quantityBefore + quantityChange)
     *     throw new InvalidOperationException("재고 계산 오류");
     *
     * 예: 100 + (-2) = 98 (정상)
     */
    public int QuantityBefore { get; set; }

    /**
     * 변동 후 수량 (Quantity After)
     *
     * 【 SQL 매핑 】
     * - 컬럼: quantity_after INT NOT NULL
     *
     * 【 데이터 무결성 】
     * quantity_after = quantity_before + quantity_change
     *
     * 【 SQL 검증 쿼리 】
     * -- 재고 계산 오류 검출
     * SELECT * FROM inventory_logs
     * WHERE quantity_after != quantity_before + quantity_change;
     *
     * → 결과가 없어야 정상 (데이터 무결성 유지)
     */
    public int QuantityAfter { get; set; }

    /**
     * 참조 ID (Reference ID) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: reference_id BIGINT NULL
     *
     * 【 .NET Nullable 개념 】
     * - long?: Nullable<long>
     * - null: 참조 없음 (수동 조정)
     * - 값 있음: 관련 엔티티 ID
     *
     * 【 실전 활용 】
     * - change_type = 'sale': reference_id = order_id (주문 번호)
     * - change_type = 'return': reference_id = order_id (반품된 주문)
     * - change_type = 'restock': reference_id = purchase_order_id (발주 번호)
     * - change_type = 'adjustment': reference_id = null (수동 조정)
     *
     * 【 SQL 예시 】
     * -- 특정 주문으로 인한 재고 변동 조회
     * SELECT * FROM inventory_logs
     * WHERE change_type = 'sale'
     *   AND reference_id = 12345; -- order_id
     *
     * -- 주문 취소 시 재고 복구 로직
     * INSERT INTO inventory_logs (product_id, change_type, quantity_change, reference_id, ...)
     * SELECT product_id, 'return', quantity, 12345, ...
     * FROM order_items
     * WHERE order_id = 12345;
     */
    public long? ReferenceId { get; set; }

    /**
     * 비고 (Notes) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: notes TEXT NULL
     *
     * 【 실전 활용 】
     * - 입고: "공장 입고 2026-03-01"
     * - 판매: "주문번호 ORD-12345"
     * - 조정: "실사 결과 파손 5개 발견"
     * - 반품: "고객 변심 반품"
     *
     * 【 관리자 메모 】
     * - 재고 오차 원인 설명
     * - 특이사항 기록
     * - 담당자 정보
     */
    public string? Notes { get; set; }

    /**
     * 생성 일시 (재고 변동 발생 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     * - INDEX: idx_inventory_logs_created_at (시계열 조회)
     *
     * 【 실전 쿼리 】
     * -- 시간대별 재고 변동 (판매 패턴 분석)
     * SELECT EXTRACT(HOUR FROM created_at) AS hour,
     *        COUNT(*) AS sale_count
     * FROM inventory_logs
     * WHERE change_type = 'sale'
     *   AND DATE(created_at) = CURRENT_DATE
     * GROUP BY hour
     * ORDER BY hour;
     *
     * 결과: 14시~18시 판매 집중 (오후 시간대)
     *
     * -- 월별 재고 회전율
     * SELECT DATE_TRUNC('month', created_at) AS month,
     *        SUM(CASE WHEN change_type = 'sale' THEN -quantity_change END) AS total_sold
     * FROM inventory_logs
     * WHERE product_id = 123
     * GROUP BY month
     * ORDER BY month DESC;
     */
    public DateTime CreatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 상품 정보 (N:1 관계)
     *
     * 【 관계 】
     * - inventory_logs (N) → products (1)
     * - 한 상품은 여러 재고 변동 이력을 가짐
     *
     * 【 SQL JOIN 예시 】
     * SELECT il.*, p.product_name, p.sku
     * FROM inventory_logs il
     * JOIN products p ON il.product_id = p.id
     * WHERE il.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * InventoryLog log = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"상품명: {log.Product?.ProductName}");
     * Console.WriteLine($"변동 유형: {log.ChangeType}");
     * Console.WriteLine($"변동 수량: {log.QuantityChange}");
     * Console.WriteLine($"변동 후 재고: {log.QuantityAfter}개");
     *
     * 【 재고 변동 이력 추적 】
     * Product product = await productRepository.GetByIdAsync(123);
     * var logs = product.InventoryLogs?
     *     .OrderByDescending(l => l.CreatedAt)
     *     .Take(10);
     *
     * foreach (var log in logs ?? Enumerable.Empty<InventoryLog>())
     * {
     *     Console.WriteLine($"{log.CreatedAt:yyyy-MM-dd HH:mm} | {log.ChangeType} | {log.QuantityChange:+#;-#;0} | 재고: {log.QuantityAfter}");
     * }
     *
     * 출력 예시:
     * 2026-03-01 14:30 | sale | -2 | 재고: 98
     * 2026-03-01 10:00 | restock | +100 | 재고: 100
     * 2026-02-28 18:45 | sale | -3 | 재고: 0
     */
    public Product? Product { get; set; }
}
