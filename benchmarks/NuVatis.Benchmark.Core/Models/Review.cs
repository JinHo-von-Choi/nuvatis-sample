namespace NuVatis.Benchmark.Core.Models;

/**
 * 리뷰 엔티티 (Entity) - 상품 후기/평점
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - 전자상거래의 핵심 기능 (사용자 신뢰도, 구매 결정에 영향)
 *
 * 【 테이블 정보 】
 * - 테이블명: reviews
 * - 레코드 수: 5,000,000개 (500만 개 리뷰)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key:
 *   - user_id → users.id (작성자)
 *   - product_id → products.id (상품)
 *
 * 【 비즈니스 중요도 】
 * - 구매 전환율: 리뷰 많은 상품 → 구매율 30% 증가
 * - 평점: 4.5점 이상 → 신뢰도 높음
 * - SEO: 리뷰 콘텐츠 → 검색 엔진 노출 증가
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Review
{
    /**
     * 리뷰 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가
     */
    public long Id { get; set; }

    /**
     * 작성자 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: user_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (user_id) REFERENCES users(id)
     * - INDEX: idx_reviews_user_id (사용자별 리뷰 조회)
     *
     * 【 SQL 예시 】
     * -- 특정 사용자의 모든 리뷰 조회
     * SELECT r.*, p.product_name
     * FROM reviews r
     * JOIN products p ON r.product_id = p.id
     * WHERE r.user_id = 12345
     * ORDER BY r.created_at DESC;
     *
     * -- 사용자별 리뷰 수
     * SELECT user_id, COUNT(*) AS review_count
     * FROM reviews
     * GROUP BY user_id
     * ORDER BY review_count DESC
     * LIMIT 10; -- TOP 10 리뷰어
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_reviews_user_product
     * ON reviews(user_id, product_id);
     * → 동일 사용자의 동일 상품 중복 리뷰 방지 검증
     */
    public long UserId { get; set; }

    /**
     * 상품 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: product_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (product_id) REFERENCES products(id)
     * - INDEX: idx_reviews_product_id (상품별 리뷰 조회)
     *
     * 【 복합 인덱스 (성능 최적화) 】
     * CREATE INDEX idx_reviews_product_created
     * ON reviews(product_id, created_at DESC);
     * → 상품별 최신 리뷰 빠르게 조회
     *
     * 【 SQL 예시 】
     * -- 특정 상품의 모든 리뷰 조회
     * SELECT r.*, u.user_name
     * FROM reviews r
     * JOIN users u ON r.user_id = u.id
     * WHERE r.product_id = 123
     * ORDER BY r.created_at DESC
     * LIMIT 10;
     *
     * -- 상품 평점 계산
     * SELECT product_id,
     *        AVG(rating) AS avg_rating,
     *        COUNT(*) AS review_count
     * FROM reviews
     * WHERE product_id = 123
     * GROUP BY product_id;
     *
     * -- 평점 분포 (별점별 개수)
     * SELECT rating, COUNT(*) AS count
     * FROM reviews
     * WHERE product_id = 123
     * GROUP BY rating
     * ORDER BY rating DESC;
     *
     * 결과:
     * | rating | count |
     * |--------|-------|
     * | 5      | 150   | ← 5점 150개
     * | 4      | 80    |
     * | 3      | 20    |
     * | 2      | 5     |
     * | 1      | 3     |
     */
    public long ProductId { get; set; }

    /**
     * 평점 (Rating) - 1~5점
     *
     * 【 SQL 매핑 】
     * - 컬럼: rating INT NOT NULL
     * - 제약: CHECK (rating >= 1 AND rating <= 5)
     *
     * 【 .NET int 타입 】
     * - int: 32비트 정수형
     * - 평점 범위: 1 (최저) ~ 5 (최고)
     *
     * 【 평점 체계 】
     * - 5점: 매우 만족 (⭐⭐⭐⭐⭐)
     * - 4점: 만족 (⭐⭐⭐⭐)
     * - 3점: 보통 (⭐⭐⭐)
     * - 2점: 불만족 (⭐⭐)
     * - 1점: 매우 불만족 (⭐)
     *
     * 【 검증 로직 】
     * if (rating < 1 || rating > 5)
     *     throw new ArgumentException("평점은 1-5 사이여야 합니다.");
     *
     * 【 평균 평점 계산 】
     * SELECT AVG(rating) AS avg_rating
     * FROM reviews
     * WHERE product_id = 123;
     *
     * 결과: 4.32 (소수점 2자리)
     *
     * 【 가중 평점 (Bayesian Average) 】
     * -- 리뷰 수가 적은 상품의 평점 보정
     * -- 예: 리뷰 1개 5점 vs 리뷰 100개 4.5점
     *
     * C = 전체 상품 평균 평점 (예: 4.0)
     * m = 최소 리뷰 수 기준 (예: 10)
     * R = 해당 상품 평균 평점
     * v = 해당 상품 리뷰 수
     *
     * 가중 평점 = (v / (v + m)) × R + (m / (v + m)) × C
     *
     * 예시:
     * - 상품 A: 평점 5.0, 리뷰 1개 → 가중 평점 4.09
     * - 상품 B: 평점 4.5, 리뷰 100개 → 가중 평점 4.45
     * → 상품 B가 더 신뢰도 높음
     */
    public int Rating { get; set; }

    /**
     * 리뷰 제목 (Title) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: title VARCHAR(200) NULL
     *
     * 【 실전 활용 】
     * - 요약: "가성비 최고입니다!"
     * - 주의점: "배송이 조금 느렸어요"
     * - SEO: 검색 엔진에 노출
     *
     * 【 .NET Nullable 개념 】
     * - string?: Nullable Reference Type (C# 8.0+)
     * - null: 제목 없음 (내용만 작성)
     * - 값 있음: 제목 + 내용
     */
    public string? Title { get; set; }

    /**
     * 리뷰 내용 (Content) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: content TEXT NULL
     *
     * 【 TEXT vs VARCHAR 】
     * - VARCHAR(N): 최대 N바이트 (짧은 텍스트)
     * - TEXT: 무제한 길이 (긴 텍스트, 최대 1GB)
     *
     * 【 실전 활용 】
     * - 상세 리뷰: 장점/단점/사용 후기 (수백~수천 자)
     * - 사진 리뷰: 텍스트 + 이미지 URL
     *
     * 【 검색 최적화 】
     * CREATE INDEX idx_reviews_content_fulltext
     * ON reviews USING gin(to_tsvector('korean', content));
     *
     * -- 키워드 검색
     * SELECT * FROM reviews
     * WHERE to_tsvector('korean', content) @@ to_tsquery('korean', '배송 & 빠르다');
     * → "배송"과 "빠르다" 모두 포함된 리뷰 검색
     *
     * 【 욕설 필터링 】
     * -- 금지어 목록: bad_words 테이블
     * -- 리뷰 작성 시 검증:
     * SELECT COUNT(*) FROM bad_words
     * WHERE @content LIKE CONCAT('%', word, '%');
     *
     * IF (COUNT > 0) THEN
     *     RAISE EXCEPTION '부적절한 단어가 포함되어 있습니다.';
     * END IF;
     */
    public string? Content { get; set; }

    /**
     * 인증 리뷰 여부 (Verified Purchase)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_verified BOOLEAN DEFAULT FALSE
     *
     * 【 인증 리뷰란? 】
     * - true: 실제 구매 고객의 리뷰 (Verified)
     * - false: 구매 이력 없는 리뷰 (Unverified)
     *
     * 【 인증 로직 】
     * -- 사용자가 실제로 해당 상품을 구매했는지 확인
     * SELECT COUNT(*) FROM order_items oi
     * JOIN orders o ON oi.order_id = o.id
     * WHERE o.user_id = @userId
     *   AND oi.product_id = @productId
     *   AND o.order_status = 'delivered'; -- 배송 완료
     *
     * IF (COUNT > 0) THEN
     *     is_verified = TRUE; -- 인증 리뷰
     * ELSE
     *     is_verified = FALSE; -- 비인증 리뷰
     * END IF;
     *
     * 【 신뢰도 향상 】
     * - 인증 리뷰: "구매 확정" 배지 표시
     * - 필터링: "인증된 리뷰만 보기"
     *
     * 【 SQL 예시 】
     * -- 인증 리뷰만 조회
     * SELECT * FROM reviews
     * WHERE product_id = 123
     *   AND is_verified = TRUE
     * ORDER BY created_at DESC;
     *
     * -- 인증 리뷰 비율
     * SELECT
     *   COUNT(CASE WHEN is_verified THEN 1 END) AS verified_count,
     *   COUNT(*) AS total_count,
     *   ROUND(COUNT(CASE WHEN is_verified THEN 1 END) * 100.0 / COUNT(*), 2) AS verified_rate
     * FROM reviews
     * WHERE product_id = 123;
     *
     * 결과: verified_rate = 85.50% (인증 리뷰 비율 85.5%)
     */
    public bool IsVerified { get; set; } = false;

    /**
     * 도움됨 수 (Helpful Count)
     *
     * 【 SQL 매핑 】
     * - 컬럼: helpful_count INT DEFAULT 0
     *
     * 【 실전 활용 】
     * - "이 리뷰가 도움이 되었나요?" 기능
     * - 사용자가 "도움됨" 버튼 클릭 시 증가
     * - 인기 리뷰 정렬: helpful_count DESC
     *
     * 【 업데이트 로직 】
     * UPDATE reviews
     * SET helpful_count = helpful_count + 1
     * WHERE id = 12345;
     *
     * 【 동시성 제어 】
     * -- 같은 사용자가 여러 번 클릭 방지
     * CREATE TABLE review_helpful (
     *     review_id BIGINT,
     *     user_id BIGINT,
     *     PRIMARY KEY (review_id, user_id)
     * );
     *
     * -- 도움됨 클릭 시:
     * BEGIN;
     * INSERT INTO review_helpful (review_id, user_id)
     * VALUES (12345, 67890); -- 중복 시 실패
     *
     * UPDATE reviews
     * SET helpful_count = helpful_count + 1
     * WHERE id = 12345;
     * COMMIT;
     *
     * 【 SQL 예시 】
     * -- 가장 도움된 리뷰 TOP 10
     * SELECT * FROM reviews
     * WHERE product_id = 123
     * ORDER BY helpful_count DESC
     * LIMIT 10;
     *
     * -- 평점별 도움됨 평균
     * SELECT rating, AVG(helpful_count) AS avg_helpful
     * FROM reviews
     * WHERE product_id = 123
     * GROUP BY rating
     * ORDER BY rating DESC;
     */
    public int HelpfulCount { get; set; } = 0;

    /**
     * 생성 일시 (리뷰 작성 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     * - INDEX: idx_reviews_created_at (시계열 조회)
     *
     * 【 실전 활용 】
     * -- 최신 리뷰 조회
     * SELECT * FROM reviews
     * WHERE product_id = 123
     * ORDER BY created_at DESC
     * LIMIT 10;
     *
     * -- 구매 후 리뷰 작성까지 걸린 시간
     * SELECT AVG(EXTRACT(DAY FROM (r.created_at - o.created_at))) AS avg_days
     * FROM reviews r
     * JOIN order_items oi ON r.product_id = oi.product_id AND r.user_id = oi.order_id
     * JOIN orders o ON oi.order_id = o.id;
     *
     * 결과: 평균 7일 (구매 후 7일 만에 리뷰 작성)
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (리뷰 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 활용 】
     * - 리뷰 수정: 오타 수정, 내용 추가
     * - 관리자 수정: 부적절한 내용 삭제
     *
     * 【 UPDATE 예시 】
     * UPDATE reviews
     * SET content = '수정된 내용',
     *     updated_at = NOW()
     * WHERE id = 12345;
     *
     * 【 수정 이력 추적 】
     * -- 별도 테이블로 수정 이력 보관
     * CREATE TABLE review_history (
     *     id BIGSERIAL PRIMARY KEY,
     *     review_id BIGINT,
     *     old_content TEXT,
     *     new_content TEXT,
     *     edited_at TIMESTAMP DEFAULT NOW()
     * );
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 작성자 정보 (N:1 관계)
     *
     * 【 관계 】
     * - reviews (N) → users (1)
     * - 한 사용자는 여러 리뷰를 작성할 수 있음
     *
     * 【 SQL JOIN 예시 】
     * SELECT r.*, u.user_name, u.email
     * FROM reviews r
     * JOIN users u ON r.user_id = u.id
     * WHERE r.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Review review = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"작성자: {review.User?.UserName}");
     */
    public User? User { get; set; }

    /**
     * 상품 정보 (N:1 관계)
     *
     * 【 관계 】
     * - reviews (N) → products (1)
     * - 한 상품은 여러 리뷰를 가질 수 있음
     *
     * 【 SQL JOIN 예시 】
     * SELECT r.*, p.product_name, p.price
     * FROM reviews r
     * JOIN products p ON r.product_id = p.id
     * WHERE r.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Review review = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"상품명: {review.Product?.ProductName}");
     * Console.WriteLine($"평점: {review.Rating}점");
     * Console.WriteLine($"내용: {review.Content}");
     *
     * 【 평균 평점 표시 】
     * Product product = await productRepository.GetByIdAsync(123);
     * var avgRating = product.Reviews?
     *     .Where(r => r.IsVerified)
     *     .Average(r => r.Rating) ?? 0;
     * Console.WriteLine($"평균 평점: {avgRating:F2}"); // 4.32
     */
    public Product? Product { get; set; }
}
