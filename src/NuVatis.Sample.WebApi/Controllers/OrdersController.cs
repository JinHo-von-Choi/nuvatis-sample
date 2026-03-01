/*
 * ==================================================================================
 * NuVatis 주문 API 컨트롤러 - 트랜잭션 및 복잡한 비즈니스 로직
 * ==================================================================================
 *
 * 이 컨트롤러는 주문 CRUD + 트랜잭션 처리의 복잡성을 보여줍니다.
 * NuVatis + ASP.NET Core Web API에서 트랜잭션을 관리하는 방법을 학습할 수 있습니다.
 *
 * ==================================================================================
 * 핵심 개념: 다중 Mapper 사용 및 트랜잭션
 * ==================================================================================
 *
 * **주문 생성 프로세스:**
 * 1. orders 테이블에 주문 기본 정보 저장
 * 2. order_items 테이블에 주문상세 여러 건 저장
 * 3. products 테이블에서 재고 차감
 *
 * **문제: 트랜잭션 부재**
 * 현재 코드는 트랜잭션이 없어서 다음 문제 발생 가능:
 *  - 주문은 생성되었는데 재고는 차감 안 됨
 *  - 주문상세는 일부만 저장됨
 *  - 데이터 불일치
 *
 * **해결: IDbTransaction 사용 (실무 패턴)**
 * NuVatis는 ADO.NET 기반이므로 IDbConnection + IDbTransaction 사용:
 *
 *     using var connection = new NpgsqlConnection(connectionString);
 *     connection.Open();
 *     using var transaction = connection.BeginTransaction();
 *     try
 *     {
 *         _orderMapper.Insert(order, transaction);
 *         _orderMapper.InsertItem(item, transaction);
 *         _productMapper.UpdateStock(id, -qty, transaction);
 *         transaction.Commit();
 *     }
 *     catch
 *     {
 *         transaction.Rollback();
 *         throw;
 *     }
 *
 * **다중 Mapper 주입:**
 * - IOrderMapper: 주문 CRUD
 * - IProductMapper: 재고 업데이트
 *
 * 이유: 주문 생성 시 재고 차감 필요
 *
 * ==================================================================================
 * association vs collection 매핑
 * ==================================================================================
 *
 * GetById: 주문 + 사용자 정보 (association)
 *  - Order.User 속성 자동 채움
 *  - INNER JOIN users
 *
 * GetByIdWithItems: 주문 + 주문상세 + 상품 (collection + nested association)
 *  - Order.Items 리스트 자동 채움
 *  - 각 OrderItem.Product 자동 채움
 *  - LEFT JOIN order_items, products
 *
 * ==================================================================================
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 * 라이센스: MIT
 *
 * ==================================================================================
 */

using Microsoft.AspNetCore.Mvc;
using NuVatis.Sample.Core.Mappers;
using NuVatis.Sample.Core.Models;

namespace NuVatis.Sample.WebApi.Controllers;

/**
 * 주문 관리 REST API 컨트롤러
 * NuVatis XML 매퍼를 사용한 주문 CRUD + 트랜잭션 처리
 * association, collection 매핑 활용
 */
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    /**
     * NuVatis 주문 매퍼
     * IOrderMapper.xml에 정의된 쿼리 실행
     */
    private readonly IOrderMapper _orderMapper;

    /**
     * NuVatis 상품 매퍼
     * 주문 생성 시 재고 차감을 위해 주입
     */
    private readonly IProductMapper _productMapper;

    /**
     * 생성자 - 다중 Mapper 주입
     * 주문 생성 시 재고 차감이 필요하므로 IProductMapper도 주입
     */
    public OrdersController(IOrderMapper orderMapper, IProductMapper productMapper)
    {
        _orderMapper = orderMapper;
        _productMapper = productMapper;
    }

    [HttpGet("{id}")]
    public ActionResult<Order> GetById(int id)
    {
        var order = _orderMapper.GetById(id);
        if (order == null)
            return NotFound();
        return Ok(order);
    }

    [HttpGet("{id}/with-items")]
    public ActionResult<Order> GetByIdWithItems(int id)
    {
        var order = _orderMapper.GetByIdWithItems(id);
        if (order == null)
            return NotFound();
        return Ok(order);
    }

    [HttpGet("user/{userId}")]
    public ActionResult<IList<Order>> GetByUserId(int userId)
    {
        var orders = _orderMapper.GetByUserId(userId);
        return Ok(orders);
    }

    /*
     * ==================================================================================
     * Create: 주문 생성 (트랜잭션 예제 - 주의: 현재 코드는 트랜잭션 없음!)
     * ==================================================================================
     *
     * HTTP 메서드: POST
     * URL: /api/orders
     * 요청 본문: { "userId": 1, "items": [{ "productId": 1, "quantity": 2 }] }
     * 응답: 201 Created + Location 헤더
     *
     * 사용 목적:
     * - 장바구니에서 주문 생성
     * - 주문 + 주문상세 + 재고 차감을 하나의 트랜잭션으로 처리
     *
     * **주의: 현재 코드의 문제점**
     *
     * 이 코드는 교육용 예제이며, 실무에서는 **절대 사용하면 안 됩니다!**
     *
     * 문제 1: 트랜잭션 부재
     *  - 주문 Insert 성공
     *  - 주문상세 InsertItem 중간에 실패
     *  - 결과: 주문은 생성되었는데 주문상세는 없음 (데이터 불일치)
     *
     * 문제 2: 재고 차감 없음
     *  - 주문은 생성되었는데 재고는 차감 안 됨
     *  - 재고 부족 확인 없음
     *
     * 문제 3: 총 금액 업데이트 없음
     *  - totalAmount 계산만 하고 DB 업데이트 안 함
     *  - order.TotalAmount는 여전히 0
     *
     * **올바른 구현 (트랜잭션 사용):**
     *
     *     [HttpPost]
     *     public async Task<ActionResult<Order>> Create([FromBody] CreateOrderRequest request)
     *     {
     *         using var connection = new NpgsqlConnection(_connectionString);
     *         await connection.OpenAsync();
     *         using var transaction = connection.BeginTransaction();
     *         try
     *         {
     *             // 1. 주문 생성
     *             var order = new Order
     *             {
     *                 UserId = request.UserId,
     *                 OrderDate = DateTime.UtcNow,
     *                 Status = "pending",
     *                 TotalAmount = 0,
     *                 CreatedAt = DateTime.UtcNow
     *             };
     *             await _orderMapper.InsertAsync(order, transaction);
     *
     *             // 2. 주문상세 생성 + 재고 차감
     *             decimal totalAmount = 0;
     *             foreach (var item in request.Items)
     *             {
     *                 var product = await _productMapper.GetByIdAsync(item.ProductId, transaction);
     *                 if (product == null)
     *                     throw new NotFoundException($"상품 {item.ProductId}를 찾을 수 없습니다.");
     *
     *                 // 재고 확인
     *                 if (product.StockQty < item.Quantity)
     *                     throw new InsufficientStockException($"상품 {product.ProductName}의 재고가 부족합니다.");
     *
     *                 // 주문상세 생성
     *                 var orderItem = new OrderItem
     *                 {
     *                     OrderId = order.Id,
     *                     ProductId = item.ProductId,
     *                     Quantity = item.Quantity,
     *                     UnitPrice = product.Price,
     *                     Subtotal = product.Price * item.Quantity
     *                 };
     *                 await _orderMapper.InsertItemAsync(orderItem, transaction);
     *
     *                 // 재고 차감 (원자적 업데이트)
     *                 await _productMapper.UpdateStockAsync(item.ProductId, -item.Quantity, transaction);
     *
     *                 totalAmount += orderItem.Subtotal;
     *             }
     *
     *             // 3. 총 금액 업데이트
     *             order.TotalAmount = totalAmount;
     *             await _orderMapper.UpdateTotalAmountAsync(order.Id, totalAmount, transaction);
     *
     *             // 4. 트랜잭션 커밋
     *             transaction.Commit();
     *
     *             return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
     *         }
     *         catch
     *         {
     *             // 5. 오류 발생 시 롤백 (재고 복원됨)
     *             transaction.Rollback();
     *             throw;
     *         }
     *     }
     *
     * **ACID 속성:**
     *
     * Atomicity (원자성):
     *  - 모두 성공 또는 모두 실패
     *  - 중간 실패 시 롤백으로 초기 상태 복원
     *
     * Consistency (일관성):
     *  - 주문 생성 → 재고 차감
     *  - 데이터베이스 제약 조건 유지
     *
     * Isolation (격리성):
     *  - 동시 트랜잭션 간 간섭 방지
     *  - 격리 수준: READ COMMITTED (기본값)
     *
     * Durability (지속성):
     *  - 커밋 후에는 시스템 장애에도 데이터 보존
     *
     * **트랜잭션 격리 수준:**
     *
     * READ UNCOMMITTED: 더티 리드 가능 (비추천)
     * READ COMMITTED: 커밋된 데이터만 읽음 (기본값, 권장)
     * REPEATABLE READ: 반복 읽기 보장
     * SERIALIZABLE: 최고 격리 수준 (성능 저하)
     *
     * 주문 생성에는 READ COMMITTED 권장
     *
     * **재고 부족 처리:**
     *
     * 옵션 1: 사전 확인 (이 예제)
     *     if (product.StockQty < item.Quantity)
     *         throw new InsufficientStockException();
     *
     * 옵션 2: UpdateStock 실패 확인
     *     var affected = await _productMapper.UpdateStockAsync(...);
     *     if (affected == 0)
     *         throw new InsufficientStockException();
     *
     * **주문번호 생성:**
     *
     * 옵션 1: 날짜 기반 + 일련번호
     *     OrderNo = $"ORD-{DateTime.Now:yyyyMMdd}-{seq:D4}"
     *     예: ORD-20260301-0001
     *
     * 옵션 2: UUID
     *     OrderNo = $"ORD-{Guid.NewGuid():N}"
     *     예: ORD-a1b2c3d4e5f6...
     *
     * 옵션 3: Snowflake ID
     *     OrderNo = $"ORD-{SnowflakeId.Generate()}"
     *
     * **CreateOrderRequest DTO:**
     *     public record CreateOrderRequest(
     *         int UserId,
     *         List<OrderItemRequest> Items
     *     );
     *
     *     public record OrderItemRequest(
     *         int ProductId,
     *         int Quantity
     *     );
     *
     * cURL 예제:
     *     curl -X POST http://localhost:5000/api/orders \
     *       -H "Content-Type: application/json" \
     *       -d '{
     *         "userId": 1,
     *         "items": [
     *           { "productId": 1, "quantity": 2 },
     *           { "productId": 2, "quantity": 1 }
     *         ]
     *       }'
     *
     * 응답 (201 Created):
     *     {
     *       "id": 123,
     *       "userId": 1,
     *       "status": "pending",
     *       "totalAmount": 50000,
     *       "orderDate": "2026-03-01T12:00:00Z"
     *     }
     *
     * **실무 추가 기능:**
     * 1. 쿠폰/할인 적용
     * 2. 배송비 계산
     * 3. 포인트 사용/적립
     * 4. 결제 처리 (PG사 연동)
     * 5. 주문 확인 이메일 발송
     * 6. 재고 부족 시 대기열 등록
     *
     * ==================================================================================
     */
    [HttpPost]
    public ActionResult<Order> Create([FromBody] CreateOrderRequest request)
    {
        // 주문 객체 생성
        var order = new Order
        {
            UserId = request.UserId,
            OrderDate = DateTime.UtcNow,
            Status = "pending",                 // 주문 상태: 결제 대기
            TotalAmount = 0,                    // 임시 값 (나중에 업데이트 필요)
            CreatedAt = DateTime.UtcNow
        };

        // 주문 저장 (orders 테이블)
        // 주의: 트랜잭션 없음! 실무에서는 트랜잭션 필수
        _orderMapper.Insert(order);

        // 주문상세 생성 + 총 금액 계산
        decimal totalAmount = 0;
        foreach (var item in request.Items)
        {
            // 상품 조회
            var product = _productMapper.GetById(item.ProductId);
            if (product == null) continue;      // 주의: 예외 처리 필요!

            // 주문상세 객체 생성
            var orderItem = new OrderItem
            {
                OrderId = order.Id,             // Insert 후 생성된 ID
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.Price,      // 주문 당시 가격 (스냅샷)
                Subtotal = product.Price * item.Quantity
            };

            // 주문상세 저장 (order_items 테이블)
            // 주의: 트랜잭션 없음! 중간 실패 시 데이터 불일치
            _orderMapper.InsertItem(orderItem);

            // 총 금액 누적
            totalAmount += orderItem.Subtotal;
        }

        // 주의: totalAmount 계산만 하고 DB 업데이트 안 함!
        // 실무에서는 _orderMapper.UpdateTotalAmount(order.Id, totalAmount) 필요

        // 201 Created 응답 + Location 헤더
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpPut("{id}/status")]
    public ActionResult UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var order = _orderMapper.GetById(id);
        if (order == null)
            return NotFound();

        _orderMapper.UpdateStatus(id, request.Status);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        var order = _orderMapper.GetById(id);
        if (order == null)
            return NotFound();

        _orderMapper.DeleteItems(id);
        _orderMapper.Delete(id);
        return NoContent();
    }
}

public record CreateOrderRequest(int UserId, List<OrderItemRequest> Items);
public record OrderItemRequest(int ProductId, int Quantity);
public record UpdateStatusRequest(string Status);
