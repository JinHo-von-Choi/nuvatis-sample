/*
 * ==================================================================================
 * OrderItem 엔티티 - 가격 스냅샷 및 nested association
 * ==================================================================================
 *
 * 이 클래스는 PostgreSQL order_items 테이블과 매핑되며,
 * **가격 스냅샷(UnitPrice)** 저장의 중요성을 보여줍니다.
 *
 * ==================================================================================
 * 테이블 스키마
 * ==================================================================================
 *
 * CREATE TABLE order_items (
 *     id          SERIAL PRIMARY KEY,
 *     order_id    INTEGER NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
 *     product_id  INTEGER NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
 *     quantity    INTEGER NOT NULL CHECK (quantity > 0),
 *     unit_price  DECIMAL(10, 2) NOT NULL CHECK (unit_price >= 0),
 *     subtotal    DECIMAL(10, 2) NOT NULL CHECK (subtotal >= 0)
 * );
 *
 * CREATE INDEX idx_order_items_order_id ON order_items(order_id);
 * CREATE INDEX idx_order_items_product_id ON order_items(product_id);
 *
 * ==================================================================================
 * 가격 스냅샷 (UnitPrice)의 중요성
 * ==================================================================================
 *
 * **문제 상황:**
 * - 2026-01-01: 노트북 가격 1,000,000원 → 주문 생성
 * - 2026-02-01: 노트북 가격 1,500,000원으로 인상
 * - 주문서 조회 시: 어떤 가격 표시?
 *
 * **잘못된 방법 (현재 가격 사용):**
 *     var orderItem = new OrderItem
 *     {
 *         ProductId = 1,
 *         Quantity = 2
 *         // UnitPrice 저장 안 함
 *     };
 *
 *     // 조회 시
 *     var product = _productMapper.GetById(orderItem.ProductId);
 *     var price = product.Price;  // 1,500,000원 (현재 가격)
 *     // 문제: 주문 당시는 1,000,000원이었음!
 *
 * **올바른 방법 (가격 스냅샷):**
 *     var product = _productMapper.GetById(1);
 *     var orderItem = new OrderItem
 *     {
 *         ProductId = product.Id,
 *         Quantity = 2,
 *         UnitPrice = product.Price,  // 주문 당시 가격 저장!
 *         Subtotal = product.Price * 2
 *     };
 *
 *     // 조회 시
 *     Console.WriteLine($"주문 당시 가격: {orderItem.UnitPrice}");
 *     // 결과: 1,000,000원 (정확함)
 *
 * **장점:**
 * - 주문 이력 정확성 보장
 * - 가격 변동과 무관하게 주문서 출력 가능
 * - 회계 감사 추적 가능
 *
 * ==================================================================================
 * 외래키 제약 조건
 * ==================================================================================
 *
 * **order_id → orders.id (ON DELETE CASCADE):**
 * - 주문 삭제 시 주문상세도 자동 삭제
 * - 이유: 주문상세는 주문에 종속적
 * - 주문 없이 주문상세만 존재할 수 없음
 *
 * **product_id → products.id (ON DELETE RESTRICT):**
 * - 상품 삭제 시 주문상세가 있으면 삭제 불가
 * - 이유: 주문 이력 보존
 * - 상품이 삭제되어도 주문서에는 상품 정보 필요
 *
 * 실무 권장:
 * - products 테이블에 is_active 컬럼 사용 (Soft Delete)
 * - 상품 삭제 대신 is_active = false로 설정
 *
 * ==================================================================================
 * 속성 설명
 * ==================================================================================
 *
 * Id (int):
 *  - 주문상세 ID (PK, Auto-increment)
 *
 * OrderId (int):
 *  - 주문 ID (FK, orders.id)
 *  - NOT NULL (주문상세는 반드시 주문에 속함)
 *
 * ProductId (int):
 *  - 상품 ID (FK, products.id)
 *  - NOT NULL
 *
 * Quantity (int):
 *  - 주문 수량
 *  - CHECK (quantity > 0): 양수만 허용
 *  - 0 또는 음수 불가
 *
 * UnitPrice (decimal):
 *  - 주문 당시 단가 (가격 스냅샷)
 *  - products.price의 복사본
 *  - 현재 상품 가격과 다를 수 있음 (정상)
 *  - CHECK (unit_price >= 0): 음수 불가
 *
 * Subtotal (decimal):
 *  - 소계 (UnitPrice * Quantity)
 *  - 미리 계산하여 저장 (성능 최적화)
 *  - 집계 쿼리 시 SUM(subtotal) 사용
 *  - CHECK (subtotal >= 0): 음수 불가
 *
 * **왜 Subtotal을 저장?**
 * - 계산 불필요: SELECT SUM(subtotal) (빠름)
 * - vs 실시간 계산: SELECT SUM(unit_price * quantity) (느림)
 * - 저장 공간 vs 성능 트레이드오프
 *
 * Product (association - nested):
 *  - 주문상세의 상품 정보
 *  - collection 안의 association
 *  - NULL 허용 (선택적 로드)
 *  - 상품이 삭제되었을 수도 있음
 *
 * ==================================================================================
 * nested association 매핑
 * ==================================================================================
 *
 * Order → OrderItem → Product (3단계 중첩)
 *
 * IOrderMapper.xml 예제:
 *     <collection property="Items" ofType="OrderItem">
 *       <id column="item_id" property="Id" />
 *       <result column="item_quantity" property="Quantity" />
 *       <result column="item_unit_price" property="UnitPrice" />
 *
 *       <!-- nested association -->
 *       <association property="Product" javaType="Product">
 *         <id column="product_id" property="Id" />
 *         <result column="product_name" property="ProductName" />
 *       </association>
 *     </collection>
 *
 * SQL 구조:
 *     SELECT
 *       o.id, o.order_no, ...,
 *       oi.id AS item_id, oi.quantity AS item_quantity, ...,
 *       p.id AS product_id, p.product_name
 *     FROM orders o
 *     LEFT JOIN order_items oi ON o.id = oi.order_id
 *     LEFT JOIN products p ON oi.product_id = p.id
 *
 * ==================================================================================
 * 사용 예제
 * ==================================================================================
 *
 * 생성 (주문 시):
 *     var product = _productMapper.GetById(1);
 *     var orderItem = new OrderItem
 *     {
 *         OrderId = order.Id,
 *         ProductId = product.Id,
 *         Quantity = 2,
 *         UnitPrice = product.Price,      // 가격 스냅샷!
 *         Subtotal = product.Price * 2
 *     };
 *     _orderMapper.InsertItem(orderItem);
 *
 * 조회 (주문서 출력):
 *     var order = _orderMapper.GetByIdWithItems(123);
 *     foreach (var item in order.Items)
 *     {
 *         Console.WriteLine($"{item.Product.ProductName}");
 *         Console.WriteLine($"  수량: {item.Quantity}");
 *         Console.WriteLine($"  단가: {item.UnitPrice:C}");      // 주문 당시 가격
 *         Console.WriteLine($"  소계: {item.Subtotal:C}");
 *     }
 *
 * ==================================================================================
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 * 라이센스: MIT
 *
 * ==================================================================================
 */

namespace NuVatis.Sample.Core.Models;

/**
 * 주문상세 엔티티
 * PostgreSQL order_items 테이블과 매핑
 * 가격 스냅샷(UnitPrice) 저장으로 주문 이력 보존
 * nested association (Product) 포함
 */
public class OrderItem
{
    /** 주문상세 ID (PK, Auto-increment) */
    public int Id { get; set; }

    /** 주문 ID (FK, orders.id, ON DELETE CASCADE) */
    public int OrderId { get; set; }

    /** 상품 ID (FK, products.id, ON DELETE RESTRICT) */
    public int ProductId { get; set; }

    /** 주문 수량 (양수만 허용) */
    public int Quantity { get; set; }

    /** 주문 당시 단가 (가격 스냅샷, 현재 상품 가격과 다를 수 있음) */
    public decimal UnitPrice { get; set; }

    /** 소계 (UnitPrice * Quantity, 미리 계산하여 저장) */
    public decimal Subtotal { get; set; }

    /**
     * 상품 정보 (nested association)
     * IOrderMapper.xml의 <collection> 안의 <association>으로 매핑
     * NULL 가능 (상품이 삭제되었을 수도 있음)
     */
    public Product? Product { get; set; }
}
