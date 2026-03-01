/*
 * ==================================================================================
 * NuVatis 상품 API 컨트롤러 - 재고 관리 및 원자적 업데이트
 * ==================================================================================
 *
 * 이 컨트롤러는 NuVatis XML 매퍼를 사용한 상품 CRUD + 재고 관리 예제입니다.
 * 특히 **원자적 재고 업데이트**를 통한 동시성 문제 해결을 보여줍니다.
 *
 * ==================================================================================
 * 핵심 개념: 재고 관리 및 동시성 제어
 * ==================================================================================
 *
 * **재고 차감 시나리오:**
 *
 * 잘못된 방법 (Read-Modify-Write):
 *     1. 현재 재고 조회: stock = 100
 *     2. 재고 계산: newStock = stock - 5 = 95
 *     3. 재고 업데이트: UPDATE ... SET stock_qty = 95
 *
 * 문제: 동시에 두 요청이 들어오면?
 *     요청 A: stock = 100, newStock = 95 (차감 5)
 *     요청 B: stock = 100, newStock = 97 (차감 3)
 *     최종 재고: 97 (실제로는 92여야 함)
 *     → 재고 부족 발생!
 *
 * **올바른 방법 (원자적 업데이트):**
 *     UPDATE products
 *     SET stock_qty = stock_qty - 5
 *     WHERE id = #{Id}
 *
 * 장점:
 *  - DB가 원자적으로 처리 (한 번에 읽고 쓰기)
 *  - 동시 요청도 순차 처리됨
 *  - 재고 정확성 보장
 *
 * **IProductMapper.xml의 UpdateStock 쿼리:**
 *     <update id="UpdateStock">
 *       UPDATE products
 *       SET stock_qty = stock_qty + #{Quantity},
 *           updated_at = CURRENT_TIMESTAMP
 *       WHERE id = #{ProductId}
 *     </update>
 *
 * 사용 예:
 *  - 재고 차감: UpdateStock(productId, -5)
 *  - 재고 증가: UpdateStock(productId, +10)
 *
 * **추가 안전장치:**
 *     <update id="UpdateStock">
 *       UPDATE products
 *       SET stock_qty = stock_qty + #{Quantity},
 *           updated_at = CURRENT_TIMESTAMP
 *       WHERE id = #{ProductId}
 *         AND stock_qty + #{Quantity} >= 0  -- 재고 음수 방지
 *     </update>
 *
 * ==================================================================================
 * PATCH vs PUT
 * ==================================================================================
 *
 * PUT /api/products/{id}:
 *  - 전체 상품 정보 수정
 *  - 모든 필드 전송 필요
 *
 * PATCH /api/products/{id}/stock:
 *  - 재고만 부분 수정
 *  - 재고 수량만 전송
 *  - RESTful 설계 원칙 준수
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
 * 상품 관리 REST API 컨트롤러
 * NuVatis XML 매퍼를 사용한 CRUD 및 재고 관리 API 제공
 * 원자적 재고 업데이트로 동시성 문제 해결
 */
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    /**
     * NuVatis 상품 매퍼 (DI로 주입됨)
     * IProductMapper.xml에 정의된 쿼리 실행
     */
    private readonly IProductMapper _productMapper;

    /**
     * 생성자 - 의존성 주입
     * ASP.NET Core DI 컨테이너가 자동으로 IProductMapper 구현체 주입
     */
    public ProductsController(IProductMapper productMapper)
    {
        _productMapper = productMapper;
    }

    /**
     * 모든 상품 조회
     * GET /api/products
     */
    [HttpGet]
    public ActionResult<IList<Product>> GetAll()
    {
        var products = _productMapper.GetAll();
        return Ok(products);
    }

    /**
     * ID로 상품 조회
     * GET /api/products/{id}
     */
    [HttpGet("{id}")]
    public ActionResult<Product> GetById(int id)
    {
        var product = _productMapper.GetById(id);
        if (product == null)
            return NotFound(new { message = "상품을 찾을 수 없습니다." });

        return Ok(product);
    }

    /**
     * 카테고리별 상품 조회
     * GET /api/products/category/{category}
     */
    [HttpGet("category/{category}")]
    public ActionResult<IList<Product>> GetByCategory(string category)
    {
        var products = _productMapper.GetByCategory(category);
        return Ok(products);
    }

    /**
     * 상품 등록
     * POST /api/products
     */
    [HttpPost]
    public ActionResult<Product> Create([FromBody] Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.IsActive  = true;

        _productMapper.Insert(product);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /**
     * 상품 수정
     * PUT /api/products/{id}
     */
    [HttpPut("{id}")]
    public ActionResult Update(int id, [FromBody] Product product)
    {
        var existing = _productMapper.GetById(id);
        if (existing == null)
            return NotFound(new { message = "상품을 찾을 수 없습니다." });

        product.Id        = id;
        product.UpdatedAt = DateTime.UtcNow;

        _productMapper.Update(product);

        return NoContent();
    }

    /*
     * ==================================================================================
     * UpdateStock: 원자적 재고 업데이트 (동시성 문제 해결)
     * ==================================================================================
     *
     * HTTP 메서드: PATCH
     * URL: /api/products/{id}/stock
     * 요청 본문: { "quantity": -5 }
     * 응답: 204 No Content
     *
     * 사용 목적:
     * - 주문 생성 시 재고 차감
     * - 주문 취소 시 재고 복원
     * - 입고 시 재고 증가
     *
     * **원자적 업데이트의 중요성:**
     *
     * 시나리오: 재고 100개, 동시에 2명이 5개씩 주문
     *
     * 잘못된 방법 (Read-Modify-Write, 경쟁 조건):
     *     // 요청 A
     *     var product = GetById(1);  // stock_qty = 100
     *     product.StockQty -= 5;      // 95
     *     Update(product);            // UPDATE ... SET stock_qty = 95
     *
     *     // 요청 B (동시 실행)
     *     var product = GetById(1);  // stock_qty = 100 (아직 A의 업데이트 전)
     *     product.StockQty -= 3;      // 97
     *     Update(product);            // UPDATE ... SET stock_qty = 97
     *
     *     최종 재고: 97 (잘못됨! 실제로는 92여야 함)
     *     → Lost Update 문제
     *
     * 올바른 방법 (원자적 업데이트, 이 API):
     *     // 요청 A
     *     UpdateStock(1, -5);  // UPDATE ... SET stock_qty = stock_qty - 5
     *
     *     // 요청 B (동시 실행)
     *     UpdateStock(1, -3);  // UPDATE ... SET stock_qty = stock_qty - 3
     *
     *     최종 재고: 92 (정확함!)
     *     → DB가 순차적으로 처리
     *
     * **SQL 실행 순서 (DB 내부):**
     *     1. 요청 A: UPDATE products SET stock_qty = stock_qty - 5 WHERE id = 1
     *        → stock_qty: 100 → 95 (DB 락 획득)
     *     2. 요청 B: UPDATE products SET stock_qty = stock_qty - 3 WHERE id = 1
     *        → 대기 (A가 락 해제할 때까지)
     *     3. 요청 A 완료 (락 해제)
     *     4. 요청 B 실행: stock_qty = stock_qty - 3
     *        → stock_qty: 95 → 92
     *
     * **NuVatis XML 매퍼 (IProductMapper.xml):**
     *     <update id="UpdateStock">
     *       UPDATE products
     *       SET stock_qty = stock_qty + #{Quantity},
     *           updated_at = CURRENT_TIMESTAMP
     *       WHERE id = #{ProductId}
     *     </update>
     *
     * 파라미터:
     *  - ProductId: 상품 ID
     *  - Quantity: 증감량 (양수: 증가, 음수: 감소)
     *
     * **재고 음수 방지:**
     *
     * 옵션 1: SQL WHERE 절에 조건 추가
     *     <update id="UpdateStock">
     *       UPDATE products
     *       SET stock_qty = stock_qty + #{Quantity},
     *           updated_at = CURRENT_TIMESTAMP
     *       WHERE id = #{ProductId}
     *         AND stock_qty + #{Quantity} >= 0
     *     </update>
     *
     *     업데이트 실패 시 (재고 부족):
     *      - 영향받은 행 수: 0
     *      - 클라이언트에서 확인 필요
     *
     * 옵션 2: CHECK 제약 조건 (DB 레벨)
     *     ALTER TABLE products
     *     ADD CONSTRAINT chk_stock_qty CHECK (stock_qty >= 0);
     *
     *     재고 음수 시도 시:
     *      - DB 예외 발생
     *      - 트랜잭션 롤백
     *
     * **사용 예제:**
     *
     * 1. 주문 생성 시 재고 차감:
     *     curl -X PATCH http://localhost:5000/api/products/1/stock \
     *       -H "Content-Type: application/json" \
     *       -d '{"quantity": -5}'
     *
     * 2. 주문 취소 시 재고 복원:
     *     curl -X PATCH http://localhost:5000/api/products/1/stock \
     *       -H "Content-Type: application/json" \
     *       -d '{"quantity": 5}'
     *
     * 3. 입고 시 재고 증가:
     *     curl -X PATCH http://localhost:5000/api/products/1/stock \
     *       -H "Content-Type: application/json" \
     *       -d '{"quantity": 100}'
     *
     * **트랜잭션과 함께 사용 (주문 생성):**
     *     using var transaction = _connection.BeginTransaction();
     *     try
     *     {
     *         // 1. 주문 생성
     *         _orderMapper.Insert(order);
     *
     *         // 2. 주문상세 생성 + 재고 차감
     *         foreach (var item in orderItems)
     *         {
     *             _orderMapper.InsertItem(item);
     *             _productMapper.UpdateStock(item.ProductId, -item.Quantity);  // 원자적 차감
     *         }
     *
     *         transaction.Commit();
     *     }
     *     catch
     *     {
     *         transaction.Rollback();  // 재고 복원됨
     *         throw;
     *     }
     *
     * **성능 비교:**
     *
     * Read-Modify-Write 방식:
     *  - 쿼리 횟수: 3번 (SELECT, 계산, UPDATE)
     *  - 락 유지 시간: 긴 (애플리케이션 로직 포함)
     *  - 동시성: 낮음 (경쟁 조건 발생)
     *
     * 원자적 업데이트 방식 (이 방식):
     *  - 쿼리 횟수: 1번 (UPDATE만)
     *  - 락 유지 시간: 짧음 (DB 내부만)
     *  - 동시성: 높음 (순차 처리 보장)
     *
     * **낙관적 잠금 vs 원자적 업데이트:**
     *
     * 낙관적 잠금 (Optimistic Locking):
     *  - Version 컬럼 사용
     *  - 충돌 시 재시도 필요
     *  - 복잡한 로직에 적합
     *
     * 원자적 업데이트 (이 방식):
     *  - Version 컬럼 불필요
     *  - 자동으로 순차 처리
     *  - 재고 업데이트에 최적
     *
     * **실무 개선 사항:**
     *     [HttpPatch("{id}/stock")]
     *     public async Task<ActionResult> UpdateStock(int id, [FromBody] StockUpdateRequest request)
     *     {
     *         var product = await _productMapper.GetByIdAsync(id);
     *         if (product == null)
     *             return NotFound();
     *
     *         // 재고 부족 사전 확인
     *         if (request.Quantity < 0 && product.StockQty < Math.Abs(request.Quantity))
     *             return BadRequest(new { message = "재고가 부족합니다." });
     *
     *         // 원자적 업데이트
     *         var affected = await _productMapper.UpdateStockAsync(id, request.Quantity);
     *
     *         // 업데이트 실패 확인 (재고 음수 방지 WHERE 절 사용 시)
     *         if (affected == 0)
     *             return BadRequest(new { message = "재고 업데이트 실패 (재고 부족 또는 상품 없음)" });
     *
     *         return NoContent();
     *     }
     *
     * **모니터링 및 알림:**
     *  - 재고 부족 알림: stock_qty < 10
     *  - 재고 0 알림: stock_qty = 0
     *  - 대량 차감 알림: Quantity < -100 (부정 주문 감지)
     *
     * ==================================================================================
     */
    [HttpPatch("{id}/stock")]
    public ActionResult UpdateStock(int id, [FromBody] StockUpdateRequest request)
    {
        // 상품 존재 확인
        var product = _productMapper.GetById(id);
        if (product == null)
            return NotFound(new { message = "상품을 찾을 수 없습니다." });

        // 원자적 재고 업데이트 (동시성 안전)
        // SQL: UPDATE products SET stock_qty = stock_qty + #{Quantity} WHERE id = #{Id}
        _productMapper.UpdateStock(id, request.Quantity);

        // 204 No Content 응답
        return NoContent();
    }

    /**
     * 상품 삭제
     * DELETE /api/products/{id}
     */
    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        var existing = _productMapper.GetById(id);
        if (existing == null)
            return NotFound(new { message = "상품을 찾을 수 없습니다." });

        _productMapper.Delete(id);

        return NoContent();
    }
}

/**
 * 재고 업데이트 요청 DTO
 */
public record StockUpdateRequest(int Quantity);
