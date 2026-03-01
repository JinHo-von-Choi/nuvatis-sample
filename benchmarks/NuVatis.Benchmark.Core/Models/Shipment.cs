namespace NuVatis.Benchmark.Core.Models;

/**
 * 배송 엔티티 (Entity) - 주문 배송 정보 (1:1 with Order)
 *
 * 【 테이블 정보 】
 * - 테이블명: shipments
 * - 레코드 수: 10,000,000개 (주문당 1개)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: order_id → orders.id (1:1 관계)
 *
 * 【 1:1 관계 】
 * - 한 주문은 하나의 배송 정보만 가짐
 * - order_id에 UNIQUE 제약 조건
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Shipment
{
    /**
     * 배송 고유 식별자 (Primary Key)
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
     * - UNIQUE: 한 주문당 1개 배송만 허용
     *
     * 【 1:1 관계 보장 】
     * CREATE UNIQUE INDEX idx_shipments_order_id ON shipments(order_id);
     * → 동일 order_id로 2개 이상 배송 생성 불가
     */
    public long OrderId { get; set; }

    /**
     * 송장 번호 (Tracking Number) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: tracking_number VARCHAR(50) NULL
     * - INDEX: idx_shipments_tracking (송장 번호 조회)
     *
     * 【 실전 활용 】
     * - 택배 조회: "1234567890123"
     * - 고객이 배송 추적
     * - 택배사 API 연동
     *
     * 【 .NET Nullable 개념 】
     * - string?: Nullable Reference Type
     * - null: 배송 준비 중 (아직 택배사 인계 전)
     * - 값 있음: 배송 시작 (택배사 인계 완료)
     *
     * 【 SQL 예시 】
     * -- 송장 번호로 배송 조회
     * SELECT s.*, o.order_number
     * FROM shipments s
     * JOIN orders o ON s.order_id = o.id
     * WHERE s.tracking_number = '1234567890123';
     */
    public string? TrackingNumber { get; set; }

    /**
     * 택배사 (Carrier) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: carrier VARCHAR(50) NULL
     *
     * 【 택배사 종류 】
     * - "CJ대한통운"
     * - "롯데택배"
     * - "한진택배"
     * - "우체국택배"
     * - "쿠팡로켓배송"
     *
     * 【 실전 활용 】
     * -- 택배사별 배송 현황
     * SELECT carrier,
     *        COUNT(*) AS shipment_count,
     *        COUNT(CASE WHEN shipment_status = 'delivered' THEN 1 END) AS delivered_count
     * FROM shipments
     * WHERE shipped_at >= '2026-03-01'
     * GROUP BY carrier
     * ORDER BY shipment_count DESC;
     *
     * 【 택배사 API 연동 】
     * // 배송 조회 API
     * var response = await CarrierClient.TrackAsync(
     *     carrier: "CJ대한통운",
     *     trackingNumber: "1234567890123"
     * );
     *
     * shipment.ShipmentStatus = response.Status; // "in_transit"
     */
    public string? Carrier { get; set; }

    /**
     * 배송 상태 (Shipment Status)
     *
     * 【 SQL 매핑 】
     * - 컬럼: shipment_status VARCHAR(20) NOT NULL
     * - 제약: CHECK (shipment_status IN ('preparing', 'shipped', 'in_transit', 'delivered'))
     *
     * 【 상태 전이 (State Transition) 】
     * preparing → shipped → in_transit → delivered
     *
     * 【 각 상태 설명 】
     * - preparing: 배송 준비 중 (상품 포장, 송장 출력)
     * - shipped: 배송 시작 (택배사 인계 완료)
     * - in_transit: 배송 중 (운송 중, 집화, 간선, 배송)
     * - delivered: 배송 완료 (고객 수령 완료)
     *
     * 【 실전 쿼리 】
     * -- 배송 현황 조회
     * SELECT shipment_status,
     *        COUNT(*) AS count
     * FROM shipments
     * WHERE created_at >= '2026-03-01'
     * GROUP BY shipment_status
     * ORDER BY
     *   CASE shipment_status
     *     WHEN 'preparing' THEN 1
     *     WHEN 'shipped' THEN 2
     *     WHEN 'in_transit' THEN 3
     *     WHEN 'delivered' THEN 4
     *   END;
     *
     * 결과:
     * | shipment_status | count  |
     * |-----------------|--------|
     * | preparing       | 1,000  |
     * | shipped         | 2,000  |
     * | in_transit      | 3,000  |
     * | delivered       | 50,000 |
     *
     * 【 배송 지연 감지 】
     * -- 3일 이상 배송 중인 주문 (배송 지연)
     * SELECT * FROM shipments
     * WHERE shipment_status IN ('shipped', 'in_transit')
     *   AND shipped_at < NOW() - INTERVAL '3 days';
     */
    public string ShipmentStatus { get; set; } = string.Empty;

    /**
     * 배송 시작 일시 (Shipped At) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: shipped_at TIMESTAMP NULL
     *
     * 【 .NET Nullable 개념 】
     * - DateTime?: Nullable<DateTime>
     * - null: 배송 준비 중 (preparing)
     * - 값 있음: 배송 시작 (shipped, in_transit, delivered)
     *
     * 【 업데이트 예시 】
     * -- 배송 시작 시
     * UPDATE shipments
     * SET shipment_status = 'shipped',
     *     shipped_at = NOW(),
     *     updated_at = NOW()
     * WHERE id = 12345;
     *
     * -- 주문 상태도 동기화
     * UPDATE orders
     * SET order_status = 'shipped',
     *     updated_at = NOW()
     * WHERE id = (SELECT order_id FROM shipments WHERE id = 12345);
     *
     * 【 실전 활용 】
     * -- 배송 준비 소요 시간 분석
     * SELECT AVG(EXTRACT(EPOCH FROM (shipped_at - created_at)) / 3600) AS avg_hours
     * FROM shipments
     * WHERE shipment_status != 'preparing';
     *
     * 결과: 평균 24시간 (주문 → 배송 시작)
     */
    public DateTime? ShippedAt { get; set; }

    /**
     * 배송 완료 일시 (Delivered At) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: delivered_at TIMESTAMP NULL
     *
     * 【 .NET Nullable 개념 】
     * - DateTime?: Nullable<DateTime>
     * - null: 배송 미완료 (preparing, shipped, in_transit)
     * - 값 있음: 배송 완료 (delivered)
     *
     * 【 업데이트 예시 】
     * -- 배송 완료 시
     * BEGIN;
     * UPDATE shipments
     * SET shipment_status = 'delivered',
     *     delivered_at = NOW(),
     *     updated_at = NOW()
     * WHERE id = 12345;
     *
     * UPDATE orders
     * SET order_status = 'delivered',
     *     updated_at = NOW()
     * WHERE id = (SELECT order_id FROM shipments WHERE id = 12345);
     * COMMIT;
     *
     * 【 실전 활용 】
     * -- 배송 소요 시간 분석
     * SELECT AVG(EXTRACT(EPOCH FROM (delivered_at - shipped_at)) / 3600) AS avg_delivery_hours
     * FROM shipments
     * WHERE shipment_status = 'delivered';
     *
     * 결과: 평균 48시간 (배송 시작 → 배송 완료)
     *
     * -- 주문부터 배송까지 전체 소요 시간
     * SELECT AVG(EXTRACT(EPOCH FROM (s.delivered_at - o.created_at)) / 3600) AS avg_total_hours
     * FROM shipments s
     * JOIN orders o ON s.order_id = o.id
     * WHERE s.shipment_status = 'delivered';
     *
     * 결과: 평균 72시간 (주문 생성 → 배송 완료)
     */
    public DateTime? DeliveredAt { get; set; }

    /**
     * 생성 일시 (배송 정보 생성 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 주문 생성 직후 또는 결제 완료 후 생성
     * - shipment_status = 'preparing' (초기 상태)
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (배송 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 상태 변경: preparing → shipped → in_transit → delivered
     * - 송장 번호 업데이트
     * - 배송 완료 시각 기록
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 주문 정보 (1:1 관계)
     *
     * 【 관계 】
     * - shipments (1) ↔ orders (1)
     * - 한 배송은 하나의 주문에 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT s.*, o.order_number, o.order_status
     * FROM shipments s
     * JOIN orders o ON s.order_id = o.id
     * WHERE s.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Shipment shipment = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"주문번호: {shipment.Order?.OrderNumber}");
     * Console.WriteLine($"송장번호: {shipment.TrackingNumber}");
     * Console.WriteLine($"택배사: {shipment.Carrier}");
     * Console.WriteLine($"배송 상태: {shipment.ShipmentStatus}");
     *
     * if (shipment.DeliveredAt.HasValue)
     * {
     *     var deliveryDays = (shipment.DeliveredAt.Value - shipment.ShippedAt!.Value).Days;
     *     Console.WriteLine($"배송 소요일: {deliveryDays}일");
     * }
     */
    public Order? Order { get; set; }
}
