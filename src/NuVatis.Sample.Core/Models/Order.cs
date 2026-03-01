/*
 * ==================================================================================
 * Order 엔티티 - NuVatis association/collection 매핑 예제
 * ==================================================================================
 *
 * 이 클래스는 PostgreSQL orders 테이블과 매핑되며,
 * NuVatis의 **association**(1:1)과 **collection**(1:N) 매핑을 보여줍니다.
 *
 * ==================================================================================
 * 테이블 스키마
 * ==================================================================================
 *
 * CREATE TABLE orders (
 *     id           SERIAL PRIMARY KEY,
 *     order_no     VARCHAR(50) NOT NULL UNIQUE,
 *     user_id      INTEGER NOT NULL REFERENCES users(id),
 *     total_amount DECIMAL(10, 2) NOT NULL DEFAULT 0,
 *     status       VARCHAR(20) NOT NULL DEFAULT 'pending',
 *     order_date   TIMESTAMP NOT NULL,
 *     shipped_date TIMESTAMP,
 *     created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
 *     updated_at   TIMESTAMP
 * );
 *
 * CREATE INDEX idx_orders_user_id ON orders(user_id);
 * CREATE INDEX idx_orders_status ON orders(status);
 * CREATE INDEX idx_orders_order_date ON orders(order_date);
 *
 * ==================================================================================
 * 관계 속성 (Navigation Properties)
 * ==================================================================================
 *
 * **User (association - 1:1):**
 *  - 하나의 Order는 하나의 User와 연결
 *  - orders.user_id → users.id (외래키)
 *  - IOrderMapper.xml의 <association> 태그로 매핑
 *  - NULL 허용 (선택적 로드)
 *
 * XML 매핑 예제:
 *     <association property="User" javaType="User">
 *       <id column="user_id" property="Id" />
 *       <result column="user_name" property="UserName" />
 *     </association>
 *
 * 사용 예제:
 *     var order = _orderMapper.GetById(123);
 *     Console.WriteLine($"주문자: {order.User.FullName}");
 *
 * **Items (collection - 1:N):**
 *  - 하나의 Order는 여러 OrderItem을 가짐
 *  - order_items.order_id → orders.id (외래키)
 *  - IOrderMapper.xml의 <collection> 태그로 매핑
 *  - 빈 리스트 초기화 (= new())
 *
 * XML 매핑 예제:
 *     <collection property="Items" ofType="OrderItem">
 *       <id column="item_id" property="Id" />
 *       <result column="item_quantity" property="Quantity" />
 *     </collection>
 *
 * 사용 예제:
 *     var order = _orderMapper.GetByIdWithItems(123);
 *     Console.WriteLine($"주문상세 {order.Items.Count}개");
 *     foreach (var item in order.Items)
 *     {
 *         Console.WriteLine($"- {item.Product.ProductName} x {item.Quantity}");
 *     }
 *
 * ==================================================================================
 * 주문 상태 (Status) 관리
 * ==================================================================================
 *
 * 가능한 상태 값:
 *  - "pending": 주문 생성 (결제 대기)
 *  - "paid": 결제 완료 (배송 대기)
 *  - "shipped": 배송 중
 *  - "delivered": 배송 완료
 *  - "cancelled": 주문 취소
 *  - "refunded": 환불 완료
 *
 * 상태 전환 규칙 (FSM: Finite State Machine):
 *  - pending → paid (결제)
 *  - paid → shipped (배송 시작)
 *  - shipped → delivered (배송 완료)
 *  - pending/paid → cancelled (취소)
 *  - cancelled → refunded (환불)
 *
 * 잘못된 전환:
 *  - delivered → cancelled (배송 완료 후 취소 불가)
 *  - shipped → pending (역방향 불가)
 *
 * ENUM 사용 권장:
 *     public enum OrderStatus
 *     {
 *         Pending, Paid, Shipped, Delivered, Cancelled, Refunded
 *     }
 *     public OrderStatus Status { get; set; } = OrderStatus.Pending;
 *
 * ==================================================================================
 * 속성 설명
 * ==================================================================================
 *
 * Id (int):
 *  - 주문 ID (PK, Auto-increment)
 *
 * OrderNo (string):
 *  - 주문번호 (비즈니스 키, UNIQUE)
 *  - 예: "ORD-20260301-0001"
 *  - 자동 생성 권장 (날짜 + 일련번호 또는 UUID)
 *
 * UserId (int):
 *  - 주문한 사용자 ID (FK, users.id)
 *  - NOT NULL (주문은 반드시 사용자에게 속함)
 *
 * TotalAmount (decimal):
 *  - 총 주문 금액
 *  - 주문상세 Subtotal의 합계
 *  - 기본값: 0 (나중에 계산 후 업데이트)
 *
 * Status (string):
 *  - 주문 상태
 *  - 기본값: "PENDING"
 *
 * OrderDate (DateTime):
 *  - 주문 일시
 *  - 생성 시 설정 (DateTime.UtcNow)
 *
 * ShippedDate (DateTime?):
 *  - 배송 시작 일시
 *  - NULL 허용 (배송 전에는 NULL)
 *  - Status가 "shipped"로 변경 시 설정
 *
 * CreatedAt (DateTime):
 *  - 생성 일시
 *
 * UpdatedAt (DateTime?):
 *  - 수정 일시
 *
 * ==================================================================================
 * N+1 문제와 해결
 * ==================================================================================
 *
 * **N+1 문제:**
 * - 주문 목록 N개 조회 후, 각 주문의 Items를 개별 쿼리로 조회
 * - 결과: 1 (주문 목록) + N (각 주문의 Items) = N+1번의 쿼리
 *
 * **해결 방법:**
 * - IOrderMapper.xml의 GetByIdWithItems 쿼리 사용
 * - LEFT JOIN으로 한 번에 조회
 * - collection 매핑으로 자동 그룹화
 *
 * ==================================================================================
 * 사용 예제
 * ==================================================================================
 *
 * 생성:
 *     var order = new Order
 *     {
 *         OrderNo = "ORD-20260301-0001",
 *         UserId = 1,
 *         TotalAmount = 0,
 *         Status = "pending",
 *         OrderDate = DateTime.UtcNow,
 *         CreatedAt = DateTime.UtcNow
 *     };
 *     _orderMapper.Insert(order);
 *
 * 조회 (User 포함):
 *     var order = _orderMapper.GetById(123);
 *     Console.WriteLine($"주문자: {order.User.FullName}");
 *
 * 조회 (Items 포함):
 *     var order = _orderMapper.GetByIdWithItems(123);
 *     Console.WriteLine($"주문상세: {order.Items.Count}개");
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
 * 주문 엔티티
 * PostgreSQL orders 테이블과 매핑
 * association (User), collection (Items) 매핑 포함
 */
public class Order
{
    /** 주문 ID (PK, Auto-increment) */
    public int Id { get; set; }

    /** 주문번호 (비즈니스 키, UNIQUE) */
    public string OrderNo { get; set; } = string.Empty;

    /** 주문한 사용자 ID (FK, users.id) */
    public int UserId { get; set; }

    /** 총 주문 금액 (주문상세 합계) */
    public decimal TotalAmount { get; set; }

    /** 주문 상태 (pending, paid, shipped, delivered, cancelled, refunded) */
    public string Status { get; set; } = "PENDING";

    /** 주문 일시 */
    public DateTime OrderDate { get; set; }

    /** 배송 시작 일시 (NULL 허용, shipped 상태일 때 설정) */
    public DateTime? ShippedDate { get; set; }

    /** 생성 일시 */
    public DateTime CreatedAt { get; set; }

    /** 수정 일시 (NULL 허용) */
    public DateTime? UpdatedAt { get; set; }

    /*
     * ==================================================================================
     * 관계 속성 (Navigation Properties)
     * ==================================================================================
     */

    /**
     * 주문한 사용자 (association - 1:1)
     * IOrderMapper.xml의 <association> 태그로 매핑
     * NULL 가능 (선택적 로드)
     */
    public User? User { get; set; }

    /**
     * 주문상세 목록 (collection - 1:N)
     * IOrderMapper.xml의 <collection> 태그로 매핑
     * 각 OrderItem은 Product 정보도 포함 (nested association)
     * 빈 리스트 초기화 (NullReferenceException 방지)
     */
    public List<OrderItem> Items { get; set; } = new();
}
