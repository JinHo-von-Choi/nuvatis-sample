using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Core.Interfaces;

/**
 * 리뷰 Repository 인터페이스 - 상품 리뷰 데이터 접근 계층
 *
 * 【 Repository 패턴 】
 * - 데이터 접근 로직을 캡슐화하여 비즈니스 로직과 분리
 * - 인터페이스로 추상화 → ORM 교체 용이 (NuVatis ↔ Dapper ↔ EF Core)
 * - 테스트 용이성 향상 (Mock Repository 사용)
 *
 * 【 벤치마크 시나리오 】
 * - Simple: GetById, WhereClause, InsertSingle
 * - Medium: TwoThreeJoin, GroupByAggregate, BulkInsert1K
 * - Complex: WindowFunctions
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public interface IReviewRepository
{
    // ========================================
    // Simple: 단순 조회
    // ========================================

    /**
     * 리뷰 ID로 단건 조회 (Primary Key 조회)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM reviews WHERE id = @id;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 예상 응답 시간: <1ms
     * - 인덱스: PRIMARY KEY (id)
     *
     * 【 사용 예시 】
     * Review? review = await repository.GetByIdAsync(12345);
     * if (review == null)
     *     throw new NotFoundException("리뷰를 찾을 수 없습니다");
     *
     * Console.WriteLine($"리뷰 ID: {review.Id}");
     * Console.WriteLine($"평점: {review.Rating}점");
     * Console.WriteLine($"내용: {review.Comment}");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - GetById
     */
    Task<Review?> GetByIdAsync(long id);

    /**
     * 상품별 리뷰 목록 조회 (WHERE 절)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM reviews
     * WHERE product_id = @productId
     * ORDER BY created_at DESC;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - product_id로 필터링
     * - 예상 응답 시간: 5-10ms (상품당 평균 100개 리뷰)
     * - 인덱스: idx_reviews_product_created (product_id, created_at DESC)
     *
     * 【 사용 예시 】
     * var reviews = await repository.GetByProductIdAsync(123);
     * foreach (var review in reviews)
     * {
     *     Console.WriteLine($"{review.Rating}점 | {review.Comment} | {review.CreatedAt:yyyy-MM-dd}");
     * }
     *
     * 【 실전 활용 】
     * - 상품 상세 페이지: 리뷰 목록 표시
     * - 리뷰 통계: 평균 평점 계산
     * - 베스트 리뷰: 높은 평점 또는 추천 많은 리뷰 필터링
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - WhereClause
     */
    Task<IEnumerable<Review>> GetByProductIdAsync(long productId);

    // ========================================
    // Medium: JOIN 및 집계 쿼리
    // ========================================

    /**
     * 리뷰 상세 조회 (2-3개 테이블 JOIN)
     *
     * 【 SQL 쿼리 】
     * SELECT r.*, u.user_name, u.email, p.product_name, p.price
     * FROM reviews r
     * JOIN users u ON r.user_id = u.id
     * JOIN products p ON r.product_id = p.id
     * WHERE r.product_id = @productId
     * ORDER BY r.created_at DESC
     * LIMIT @limit;
     *
     * 【 JOIN을 통한 관련 정보 포함 】
     * - users: 리뷰 작성자 정보 (user_name, email)
     * - products: 상품 정보 (product_name, price)
     * - 1번의 쿼리로 모든 정보 로드 (Eager Loading)
     *
     * 【 Lazy Loading vs Eager Loading 】
     * [비권장] Lazy Loading (N+1 문제):
     *   var reviews = GetByProductIdAsync(123);
     *   foreach (var review in reviews)
     *   {
     *       string userName = review.User.UserName; // 추가 쿼리 발생!
     *       string productName = review.Product.ProductName; // 추가 쿼리 발생!
     *   }
     *   총 쿼리: 1 + (N × 2)
     *
     * [권장] Eager Loading (JOIN 사용):
     *   var reviews = GetWithDetailsAsync(123, 10);
     *   → 1번의 JOIN 쿼리로 모두 로드
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - product_id로 필터링
     * - 예상 응답 시간: 5-15ms (limit 10 기준)
     * - 네트워크 왕복: 1회
     *
     * 【 사용 예시 】
     * var reviews = await repository.GetWithDetailsAsync(productId: 123, limit: 10);
     * foreach (var review in reviews)
     * {
     *     Console.WriteLine($"[{review.User?.UserName}] {review.Rating}점");
     *     Console.WriteLine($"  상품: {review.Product?.ProductName}");
     *     Console.WriteLine($"  내용: {review.Comment}");
     *     Console.WriteLine($"  작성일: {review.CreatedAt:yyyy-MM-dd}");
     *     Console.WriteLine();
     * }
     *
     * 출력 예시:
     * [홍길동] 5점
     *   상품: 게이밍 노트북
     *   내용: 성능이 아주 좋습니다!
     *   작성일: 2026-03-01
     *
     * [김철수] 4점
     *   상품: 게이밍 노트북
     *   내용: 가격 대비 만족합니다.
     *   작성일: 2026-02-28
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - TwoThreeJoin
     */
    Task<IEnumerable<Review>> GetWithDetailsAsync(long productId, int limit);

    /**
     * 상품별 평균 평점 집계 (GROUP BY + AVG)
     *
     * 【 SQL 쿼리 】
     * SELECT product_id,
     *        AVG(rating) AS avg_rating
     * FROM reviews
     * GROUP BY product_id
     * ORDER BY avg_rating DESC;
     *
     * 【 GROUP BY + 집계 함수 】
     * - GROUP BY product_id: 상품별로 그룹화
     * - AVG(rating): 각 그룹의 평균 평점 계산
     *
     * 【 집계 함수 종류 】
     * - AVG(rating): 평균 평점
     * - COUNT(*): 리뷰 개수
     * - SUM(rating): 평점 총합
     * - MAX(rating): 최고 평점
     * - MIN(rating): 최저 평점
     *
     * 【 .NET 타입 매핑 】
     * - AVG() → double (소수점 포함)
     * - COUNT() → long (정수)
     * - SUM() → long 또는 decimal (타입에 따라)
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n) - 전체 리뷰 스캔
     * - 예상 응답 시간: 50-200ms (500만 건 리뷰)
     * - 인덱스: idx_reviews_product_id (product_id)
     *
     * 【 사용 예시 】
     * var avgRatings = await repository.GetAverageRatingByProductAsync();
     * foreach (var (productId, avgRating) in avgRatings)
     * {
     *     Console.WriteLine($"상품 ID {productId}: 평균 {avgRating:F2}점");
     * }
     *
     * 출력 예시:
     * 상품 ID 123: 평균 4.85점
     * 상품 ID 456: 평균 4.50점
     * 상품 ID 789: 평균 3.20점
     *
     * 【 실전 활용 】
     * - 상품 목록: 평균 평점 표시 (★★★★☆ 4.5)
     * - 베스트 상품: 평점 높은 순 정렬
     * - 추천 시스템: 평점 4점 이상 상품 추천
     * - 품질 개선: 평점 낮은 상품 파악
     *
     * 【 SQL 확장 예시 】
     * -- 평점과 리뷰 개수 함께 조회
     * SELECT product_id,
     *        AVG(rating) AS avg_rating,
     *        COUNT(*) AS review_count
     * FROM reviews
     * GROUP BY product_id
     * HAVING COUNT(*) >= 10  -- 리뷰 10개 이상인 상품만
     * ORDER BY avg_rating DESC;
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - GroupByAggregate
     */
    Task<Dictionary<long, double>> GetAverageRatingByProductAsync();

    // ========================================
    // Complex: Window Functions
    // ========================================

    /**
     * 상품별 상위 N개 리뷰 조회 (Window Function)
     *
     * 【 SQL 쿼리 】
     * SELECT r.*, u.user_name, p.product_name,
     *        ROW_NUMBER() OVER (PARTITION BY r.product_id ORDER BY r.rating DESC, r.created_at DESC) AS rank
     * FROM reviews r
     * JOIN users u ON r.user_id = u.id
     * JOIN products p ON r.product_id = p.id
     * WHERE rank <= @topN
     * ORDER BY r.product_id, rank;
     *
     * 【 Window Function 개념 】
     * - 그룹별로 순위, 누적합, 이동평균 등을 계산하는 고급 SQL 기능
     * - GROUP BY와 달리 모든 행을 유지하면서 집계 결과 추가
     * - PARTITION BY: 그룹 분할 (카테고리별, 상품별 등)
     * - ORDER BY: 순위 기준 (평점 높은 순, 날짜 최신 순)
     *
     * 【 Window Function 종류 】
     * - ROW_NUMBER(): 순차적 번호 부여 (1, 2, 3, ...)
     * - RANK(): 동점 허용 (1, 2, 2, 4, ...) - 동점 시 다음 순위 건너뜀
     * - DENSE_RANK(): 동점 허용 (1, 2, 2, 3, ...) - 동점 시에도 순위 연속
     * - SUM() OVER: 누적합
     * - AVG() OVER: 이동평균
     *
     * 【 ROW_NUMBER vs RANK vs DENSE_RANK 비교 】
     * 예시 데이터 (평점 순):
     * | product_id | rating | created_at |
     * |------------|--------|------------|
     * | 123        | 5      | 2026-03-01 |
     * | 123        | 5      | 2026-02-28 | ← 동점
     * | 123        | 4      | 2026-02-25 |
     * | 123        | 3      | 2026-02-20 |
     *
     * ROW_NUMBER() 결과:
     * | rating | rank |
     * |--------|------|
     * | 5      | 1    |
     * | 5      | 2    | ← 동점이어도 다른 순위
     * | 4      | 3    |
     * | 3      | 4    |
     *
     * RANK() 결과:
     * | rating | rank |
     * |--------|------|
     * | 5      | 1    |
     * | 5      | 1    | ← 동점은 같은 순위
     * | 4      | 3    | ← 2번 건너뜀
     * | 3      | 4    |
     *
     * DENSE_RANK() 결과:
     * | rating | rank |
     * |--------|------|
     * | 5      | 1    |
     * | 5      | 1    | ← 동점은 같은 순위
     * | 4      | 2    | ← 순위 연속
     * | 3      | 3    |
     *
     * 【 PARTITION BY 개념 】
     * - PARTITION BY product_id: 상품별로 별도 순위 부여
     * - 각 상품마다 1위, 2위, 3위가 독립적으로 존재
     *
     * 예시:
     * | product_id | rating | rank |
     * |------------|--------|------|
     * | 123        | 5      | 1    | ← 상품 123의 1위
     * | 123        | 4      | 2    | ← 상품 123의 2위
     * | 456        | 5      | 1    | ← 상품 456의 1위
     * | 456        | 3      | 2    | ← 상품 456의 2위
     *
     * 【 GROUP BY vs Window Function 비교 】
     * [제약] GROUP BY:
     *   SELECT product_id, AVG(rating)
     *   FROM reviews
     *   GROUP BY product_id;
     *
     *   결과: 각 상품당 1행만 반환 (개별 리뷰 정보 손실)
     *   | product_id | avg_rating |
     * |------------|------------|
     *   | 123        | 4.5        |
     *   | 456        | 4.0        |
     *
     * [유연] Window Function:
     *   SELECT product_id, rating,
     *          AVG(rating) OVER (PARTITION BY product_id) AS avg_rating
     *   FROM reviews;
     *
     *   결과: 모든 행 유지하면서 평균 추가
     *   | product_id | rating | avg_rating |
     *   |------------|--------|------------|
     *   | 123        | 5      | 4.5        |
     *   | 123        | 4      | 4.5        |
     *   | 456        | 5      | 4.0        |
     *   | 456        | 3      | 4.0        |
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n log n) - 정렬 필요
     * - 예상 응답 시간: 10-40ms
     * - 인덱스: idx_reviews_product_rating (product_id, rating DESC)
     *
     * 【 사용 예시 】
     * // 각 상품의 상위 3개 리뷰 조회
     * var topReviews = await repository.GetTopReviewsByProductAsync(topN: 3);
     * foreach (var review in topReviews)
     * {
     *     Console.WriteLine($"[상품 {review.product_id}] {review.rank}위");
     *     Console.WriteLine($"  평점: {review.rating}점");
     *     Console.WriteLine($"  작성자: {review.user_name}");
     *     Console.WriteLine($"  내용: {review.comment}");
     *     Console.WriteLine();
     * }
     *
     * 출력 예시:
     * [상품 123] 1위
     *   평점: 5점
     *   작성자: 홍길동
     *   내용: 정말 훌륭합니다!
     *
     * [상품 123] 2위
     *   평점: 5점
     *   작성자: 김철수
     *   내용: 강력 추천합니다.
     *
     * [상품 123] 3위
     *   평점: 4점
     *   작성자: 이영희
     *   내용: 가성비 좋아요.
     *
     * 【 실전 활용 】
     * - 베스트 리뷰: 상품별 상위 3개 리뷰만 표시
     * - 월별 TOP 10: 이달의 인기 리뷰 선정
     * - 랭킹 시스템: 사용자별 리뷰 순위 매기기
     * - 필터링: 낮은 평점 리뷰 제외 (상위 N개만)
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - WindowFunctions
     */
    Task<IEnumerable<dynamic>> GetTopReviewsByProductAsync(int topN);

    // ========================================
    // Simple: 삽입
    // ========================================

    /**
     * 리뷰 단건 삽입 (INSERT)
     *
     * 【 SQL 쿼리 】
     * INSERT INTO reviews (user_id, product_id, rating, comment, created_at)
     * VALUES (@userId, @productId, @rating, @comment, @createdAt)
     * RETURNING id;
     *
     * 【 데이터 검증 】
     * - rating: 1-5 범위 (CHECK 제약)
     * - user_id: users 테이블에 존재 (FK 제약)
     * - product_id: products 테이블에 존재 (FK 제약)
     * - comment: 최소 10자 이상 (애플리케이션 레벨 검증)
     *
     * 【 비즈니스 규칙 】
     * - 중복 리뷰 방지: 동일 사용자가 동일 상품에 여러 리뷰 작성 금지
     * - UNIQUE 제약: (user_id, product_id)
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1)
     * - 예상 응답 시간: 2-5ms
     *
     * 【 사용 예시 】
     * Review review = new Review
     * {
     *     UserId = 12345,
     *     ProductId = 123,
     *     Rating = 5,
     *     Comment = "정말 훌륭한 상품입니다. 강력 추천합니다!",
     *     CreatedAt = DateTime.UtcNow
     * };
     *
     * long reviewId = await repository.InsertAsync(review);
     * Console.WriteLine($"리뷰 작성 완료: {reviewId}");
     *
     * 【 중복 리뷰 방지 예시 】
     * try
     * {
     *     await repository.InsertAsync(review);
     * }
     * catch (DbException ex) when (ex.Message.Contains("unique constraint"))
     * {
     *     throw new InvalidOperationException("이미 해당 상품에 리뷰를 작성하셨습니다");
     * }
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - InsertSingle
     */
    Task<long> InsertAsync(Review review);

    /**
     * 리뷰 대량 삽입 (1,000건)
     *
     * 【 SQL 쿼리 (배치 INSERT) 】
     * INSERT INTO reviews (user_id, product_id, rating, comment, created_at)
     * VALUES
     *   (@userId1, @productId1, @rating1, @comment1, @createdAt1),
     *   (@userId2, @productId2, @rating2, @comment2, @createdAt2),
     *   ...
     *   (@userId1000, @productId1000, @rating1000, @comment1000, @createdAt1000);
     *
     * 【 대량 삽입 방법 비교 】
     * [비권장] 개별 INSERT (1,000번 실행):
     *   foreach (var review in reviews)
     *   {
     *       await repository.InsertAsync(review);
     *   }
     *   → 응답 시간: 2-10초 (네트워크 왕복 1,000회)
     *
     * [권장] 배치 INSERT (1번 실행):
     *   INSERT INTO reviews (...) VALUES (...), (...), ... -- 1,000개
     *   → 응답 시간: 50-200ms (네트워크 왕복 1회)
     *
     * 성능 개선: 10-50배 빠름
     *
     * 【 .NET 배치 INSERT 패턴 】
     * // Dapper
     * await connection.ExecuteAsync(
     *     "INSERT INTO reviews (...) VALUES (@UserId, @ProductId, ...)",
     *     reviews  // IEnumerable<Review>
     * );
     *
     * // NuVatis XML 매퍼
     * <insert id="BulkInsertAsync">
     *   INSERT INTO reviews (user_id, product_id, rating, comment, created_at)
     *   VALUES
     *   <foreach collection="list" item="item" separator=",">
     *     (#{item.UserId}, #{item.ProductId}, #{item.Rating}, #{item.Comment}, #{item.CreatedAt})
     *   </foreach>
     * </insert>
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(n)
     * - 예상 응답 시간: 50-200ms (1,000건)
     * - 네트워크 왕복: 1회
     *
     * 【 사용 예시 】
     * List<Review> reviews = GenerateReviews(1000);
     * int inserted = await repository.BulkInsertAsync(reviews);
     * Console.WriteLine($"{inserted}건의 리뷰가 삽입되었습니다");
     *
     * 【 대량 삽입 시 주의사항 】
     * - 메모리: 1,000건 이상 시 메모리 사용량 증가
     * - 트랜잭션: 전체를 하나의 트랜잭션으로 처리 (All or Nothing)
     * - 타임아웃: 대량 삽입 시 타임아웃 시간 증가 필요
     * - 인덱스: 대량 삽입 전 인덱스 비활성화 후 재생성 고려
     *
     * 【 벤치마크 시나리오 】
     * - CategoryB: Medium Query - BulkInsert1K
     */
    Task<int> BulkInsertAsync(IEnumerable<Review> reviews);
}
