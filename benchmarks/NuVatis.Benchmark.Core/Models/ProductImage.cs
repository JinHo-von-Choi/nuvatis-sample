namespace NuVatis.Benchmark.Core.Models;

/**
 * 상품 이미지 엔티티 (Entity) - 상품의 이미지 파일 정보
 *
 * 【 테이블 정보 】
 * - 테이블명: product_images
 * - 레코드 수: 100,000개 (상품당 평균 2개 이미지)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: product_id → products.id
 *
 * 【 실전 활용 】
 * - 대표 이미지: is_primary = true (상품 목록 썸네일)
 * - 상세 이미지: 여러 각도, 사용 예시 사진
 * - 표시 순서: display_order (1, 2, 3, ...)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class ProductImage
{
    /**
     * 이미지 고유 식별자 (Primary Key)
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
     * - INDEX: idx_product_images_product_id
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_product_images_product_order
     * ON product_images(product_id, display_order ASC);
     * → 상품별 이미지를 표시 순서대로 빠르게 조회
     */
    public long ProductId { get; set; }

    /**
     * 이미지 URL
     *
     * 【 SQL 매핑 】
     * - 컬럼: image_url VARCHAR(500) NOT NULL
     *
     * 【 저장 방식 】
     * - CDN URL: "https://cdn.example.com/products/12345/image1.jpg"
     * - S3 URL: "https://s3.amazonaws.com/bucket/products/12345.jpg"
     * - 상대 경로: "/images/products/12345/image1.jpg"
     *
     * 【 이미지 최적화 】
     * - 썸네일: 200x200 (상품 목록)
     * - 중간 크기: 800x800 (상품 상세)
     * - 원본: 2000x2000 (확대 보기)
     * - WebP 포맷: JPEG보다 30% 작은 용량
     *
     * 【 CDN 활용 】
     * - CloudFront, Cloudflare: 전 세계 캐싱
     * - 이미지 리사이징: URL 파라미터로 크기 조정
     *   "https://cdn.example.com/products/12345.jpg?w=200&h=200"
     */
    public string ImageUrl { get; set; } = string.Empty;

    /**
     * 대체 텍스트 (Alt Text) - 접근성 및 SEO
     *
     * 【 SQL 매핑 】
     * - 컬럼: alt_text VARCHAR(200) NULL
     *
     * 【 실전 활용 】
     * - 시각 장애인: 스크린 리더가 alt_text 읽음
     * - SEO: 검색 엔진이 이미지 내용 파악
     * - 이미지 로드 실패 시: alt_text 표시
     *
     * 【 좋은 Alt Text 예시 】
     * [권장] "삼성 갤럭시 S24 블랙 색상 전면 사진"
     * [비권장] "이미지", "상품 사진" (정보 부족)
     */
    public string? AltText { get; set; }

    /**
     * 표시 순서 (Display Order)
     *
     * 【 SQL 매핑 】
     * - 컬럼: display_order INT DEFAULT 0
     *
     * 【 정렬 우선순위 】
     * ORDER BY display_order ASC
     * - 0: 첫 번째 이미지
     * - 1: 두 번째 이미지
     * - 2: 세 번째 이미지
     *
     * 【 SQL 예시 】
     * SELECT * FROM product_images
     * WHERE product_id = 123
     * ORDER BY display_order ASC;
     */
    public int DisplayOrder { get; set; } = 0;

    /**
     * 대표 이미지 여부 (Primary Image)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_primary BOOLEAN DEFAULT FALSE
     *
     * 【 제약 조건 】
     * - 한 상품당 1개만 is_primary = true
     * - UNIQUE INDEX: idx_product_primary ON product_images(product_id) WHERE is_primary = TRUE;
     *
     * 【 실전 활용 】
     * -- 상품 목록 조회 시 대표 이미지만
     * SELECT p.*, pi.image_url AS thumbnail
     * FROM products p
     * LEFT JOIN product_images pi ON p.id = pi.product_id AND pi.is_primary = TRUE
     * WHERE p.is_active = TRUE;
     */
    public bool IsPrimary { get; set; } = false;

    /**
     * 생성 일시 (이미지 업로드 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     */
    public DateTime CreatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 상품 정보 (N:1 관계)
     *
     * 【 관계 】
     * - product_images (N) → products (1)
     * - 여러 이미지가 한 상품에 속함
     */
    public Product? Product { get; set; }
}
