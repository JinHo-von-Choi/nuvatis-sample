/*
 * ==================================================================================
 * Product 엔티티 - 재고 관리 및 동시성 제어
 * ==================================================================================
 *
 * 이 클래스는 PostgreSQL products 테이블과 매핑되는 엔티티입니다.
 * **재고 관리(StockQty)**는 동시성 문제가 발생하기 쉬우므로 주의가 필요합니다.
 *
 * ==================================================================================
 * 테이블 스키마
 * ==================================================================================
 *
 * CREATE TABLE products (
 *     id           SERIAL PRIMARY KEY,
 *     product_code VARCHAR(50) NOT NULL UNIQUE,
 *     product_name VARCHAR(200) NOT NULL,
 *     description  TEXT,
 *     price        DECIMAL(10, 2) NOT NULL CHECK (price >= 0),
 *     stock_qty    INTEGER NOT NULL DEFAULT 0 CHECK (stock_qty >= 0),
 *     category     VARCHAR(50) NOT NULL,
 *     created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
 *     updated_at   TIMESTAMP,
 *     is_active    BOOLEAN NOT NULL DEFAULT true
 * );
 *
 * CREATE INDEX idx_products_category ON products(category);
 * CREATE INDEX idx_products_product_code ON products(product_code);
 * CREATE INDEX idx_products_is_active ON products(is_active);
 *
 * ==================================================================================
 * 재고 관리 (StockQty) 주의사항
 * ==================================================================================
 *
 * **동시성 문제:**
 * - 여러 사용자가 동시에 같은 상품 주문 시 재고 부족 발생 가능
 * - Read-Modify-Write 패턴은 위험함
 *
 * **잘못된 방법:**
 *     var product = GetById(1);
 *     product.StockQty -= 5;  // 경쟁 조건 (Race Condition)
 *     Update(product);
 *
 * **올바른 방법 (원자적 업데이트):**
 *     UPDATE products
 *     SET stock_qty = stock_qty - 5
 *     WHERE id = 1 AND stock_qty >= 5;
 *
 * IProductMapper.xml의 UpdateStock 쿼리 사용:
 *     _productMapper.UpdateStock(productId, -5);
 *
 * ==================================================================================
 * 속성 설명
 * ==================================================================================
 *
 * Id (int):
 *  - 테이블: id SERIAL PRIMARY KEY
 *  - 자동 증가
 *
 * ProductCode (string):
 *  - 테이블: product_code VARCHAR(50) NOT NULL UNIQUE
 *  - 상품 코드 (SKU)
 *  - 중복 불가
 *  - 예: "PROD-001", "SKU-LAPTOP-001"
 *  - 인덱스 존재 (빠른 검색)
 *
 * ProductName (string):
 *  - 테이블: product_name VARCHAR(200) NOT NULL
 *  - 상품명
 *  - 예: "삼성 노트북 15인치"
 *
 * Description (string?):
 *  - 테이블: description TEXT
 *  - 상품 설명
 *  - NULL 허용
 *  - 긴 텍스트 가능 (TEXT 타입)
 *
 * Price (decimal):
 *  - 테이블: price DECIMAL(10, 2) NOT NULL CHECK (price >= 0)
 *  - 가격 (소수점 2자리)
 *  - 음수 불가 (CHECK 제약 조건)
 *  - 예: 1500000.00 (150만원)
 *  - C# decimal 타입 사용 (정확한 계산)
 *
 * **왜 decimal?**
 *  - float/double: 부동소수점 오차 발생 (금액 계산 부적합)
 *  - decimal: 정확한 10진수 계산 (금액 계산 필수)
 *  - 예: 0.1 + 0.2 = 0.30000000000000004 (float)
 *       0.1 + 0.2 = 0.3 (decimal)
 *
 * StockQty (int):
 *  - 테이블: stock_qty INTEGER NOT NULL DEFAULT 0 CHECK (stock_qty >= 0)
 *  - 재고 수량
 *  - 음수 불가 (CHECK 제약 조건)
 *  - 기본값: 0
 *  - **동시성 제어 필수!**
 *
 * Category (string):
 *  - 테이블: category VARCHAR(50) NOT NULL
 *  - 카테고리
 *  - 예: "전자제품", "의류", "식품"
 *  - 인덱스 존재 (카테고리별 검색)
 *
 * CreatedAt (DateTime):
 *  - 생성 일시
 *
 * UpdatedAt (DateTime?):
 *  - 수정 일시
 *
 * IsActive (bool):
 *  - 활성 여부
 *  - false: 판매 중단
 *
 * ==================================================================================
 * 가격 변경 이력 관리
 * ==================================================================================
 *
 * 문제: 주문 후 가격이 변경되면?
 *  - 주문 당시 가격: 10,000원
 *  - 현재 가격: 15,000원
 *  - 주문서에 어떤 가격 표시?
 *
 * 해결: 주문상세(OrderItem)에 가격 스냅샷 저장
 *  - order_items.unit_price = 주문 당시 products.price
 *  - 가격 변경과 무관하게 주문 이력 보존
 *
 * 추가 해결: 가격 변경 이력 테이블
 *     CREATE TABLE price_history (
 *         id SERIAL PRIMARY KEY,
 *         product_id INTEGER NOT NULL,
 *         old_price DECIMAL(10, 2),
 *         new_price DECIMAL(10, 2),
 *         changed_at TIMESTAMP NOT NULL
 *     );
 *
 * ==================================================================================
 * 재고 부족 알림
 * ==================================================================================
 *
 * 재고 모니터링:
 *  - stock_qty < 10: 경고 알림
 *  - stock_qty = 0: 품절 알림
 *  - stock_qty < 0: 시스템 오류 (발생하면 안 됨)
 *
 * 구현 예제:
 *     public async Task CheckLowStock()
 *     {
 *         var lowStockProducts = await _productMapper.GetLowStockAsync(10);
 *         foreach (var product in lowStockProducts)
 *         {
 *             await _notificationService.SendAsync(
 *                 $"재고 부족: {product.ProductName} (남은 수량: {product.StockQty})");
 *         }
 *     }
 *
 * ==================================================================================
 * 사용 예제
 * ==================================================================================
 *
 * 생성:
 *     var product = new Product
 *     {
 *         ProductCode = "PROD-001",
 *         ProductName = "삼성 노트북",
 *         Price = 1500000.00M,  // M 접미사: decimal 리터럴
 *         StockQty = 100,
 *         Category = "전자제품",
 *         CreatedAt = DateTime.UtcNow,
 *         IsActive = true
 *     };
 *     _productMapper.Insert(product);
 *
 * 재고 차감 (주문 시):
 *     _productMapper.UpdateStock(productId, -5);  // 5개 차감
 *
 * 재고 증가 (입고 시):
 *     _productMapper.UpdateStock(productId, 100);  // 100개 증가
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
 * 상품 엔티티
 * PostgreSQL products 테이블과 매핑
 * 재고 관리 시 동시성 제어 필수
 */
public class Product
{
    /** 상품 ID (PK, Auto-increment) */
    public int Id { get; set; }

    /** 상품 코드 (SKU, UNIQUE) */
    public string ProductCode { get; set; } = string.Empty;

    /** 상품명 */
    public string ProductName { get; set; } = string.Empty;

    /** 상품 설명 (긴 텍스트, NULL 허용) */
    public string? Description { get; set; }

    /** 가격 (DECIMAL, 정확한 금액 계산) */
    public decimal Price { get; set; }

    /** 재고 수량 (동시성 제어 필수!) */
    public int StockQty { get; set; }

    /** 카테고리 */
    public string Category { get; set; } = string.Empty;

    /** 생성 일시 */
    public DateTime CreatedAt { get; set; }

    /** 수정 일시 (NULL 허용) */
    public DateTime? UpdatedAt { get; set; }

    /** 활성 여부 (false: 판매 중단) */
    public bool IsActive { get; set; } = true;
}
