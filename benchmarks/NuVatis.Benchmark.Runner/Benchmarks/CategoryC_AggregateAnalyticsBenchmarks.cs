using BenchmarkDotNet.Attributes;
using NuVatis.Benchmark.Core.Interfaces;

namespace NuVatis.Benchmark.Runner.Benchmarks;

/**
 * 카테고리 C: 집계 및 분석 쿼리 벤치마크 - GROUP BY, Window Functions 성능 비교
 *
 * 【 벤치마크 목적 】
 * - ORM별 집계 쿼리 (GROUP BY + AVG/COUNT/SUM) 성능 비교
 * - Window Functions (ROW_NUMBER, RANK, DENSE_RANK) 성능 측정
 * - 대용량 데이터 집계 시 메모리 및 CPU 사용량 분석
 *
 * 【 시나리오 】
 * - C01-C05: 집계 쿼리 (GROUP BY + AVG) - 상품별 평균 평점 계산
 * - C06-C10: Window Functions - 상품별 상위 N개 리뷰 조회
 * - C11-C15: JOIN + GROUP BY - 국가별 사용자 수 집계
 *
 * 【 집계 함수(Aggregate Functions)란? 】
 * - 여러 행을 하나의 결과 값으로 요약
 * - GROUP BY와 함께 사용하여 그룹별 집계
 * - 종류: AVG(평균), SUM(합계), COUNT(개수), MAX(최댓값), MIN(최솟값)
 *
 * 【 GROUP BY 개념 】
 * - 동일한 값을 가진 행들을 그룹화
 * - 각 그룹에 대해 집계 함수 실행
 * - 예: 상품별 평균 평점 → product_id로 그룹화 후 AVG(rating)
 *
 * 【 Window Functions 개념 】
 * - 그룹별 순위, 누적합, 이동평균 등 계산
 * - GROUP BY와 달리 모든 행 유지
 * - PARTITION BY로 그룹 분할, ORDER BY로 순위 기준 지정
 *
 * 【 대용량 데이터 집계 특성 】
 * - reviews: 5,000,000건 (500만 건)
 * - GROUP BY 시 전체 스캔 필요 → O(n)
 * - 인덱스 활용으로 성능 개선 가능
 *
 * 【 성능 목표 】
 * - GROUP BY + AVG: 50-200ms (500만 건 스캔)
 * - Window Functions: 10-40ms (정렬 + 순위 계산)
 * - JOIN + GROUP BY: 100-300ms (JOIN 후 집계)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
[MemoryDiagnoser]
[RankColumn]
public class CategoryC_AggregateAnalyticsBenchmarks
{
    /**
     * Review Repository 인스턴스 (ORM별)
     */
    private IReviewRepository _reviewNuvatis = null!;
    private IReviewRepository _reviewDapper = null!;
    private IReviewRepository _reviewEfCore = null!;

    /**
     * User Repository 인스턴스 (ORM별)
     */
    private IUserRepository _userNuvatis = null!;
    private IUserRepository _userDapper = null!;
    private IUserRepository _userEfCore = null!;

    /**
     * 벤치마크 시작 전 초기화 (1회 실행)
     */
    [GlobalSetup]
    public void Setup()
    {
        // TODO: DI 컨테이너에서 주입
    }

    // ========================================
    // C01-C05: 집계 쿼리 (GROUP BY + AVG)
    // ========================================

    /**
     * C01: 상품별 평균 평점 집계 - NuVatis 구현
     *
     * 【 SQL 쿼리 】
     * SELECT product_id,
     *        AVG(rating) AS avg_rating
     * FROM reviews
     * GROUP BY product_id
     * ORDER BY avg_rating DESC;
     *
     * 【 GROUP BY 실행 과정 】
     * 1. 전체 reviews 테이블 스캔 (5,000,000건)
     * 2. product_id별로 그룹화
     * 3. 각 그룹의 rating 평균 계산
     * 4. avg_rating으로 정렬
     *
     * 【 집계 함수 AVG() 】
     * - 평균(Average) 계산
     * - NULL 값 제외
     * - 소수점 포함 (DOUBLE 타입)
     *
     * 【 예시 데이터 】
     * reviews 테이블:
     * | product_id | rating |
     * |------------|--------|
     * | 123        | 5      |
     * | 123        | 4      |
     * | 123        | 5      |
     * | 456        | 3      |
     * | 456        | 4      |
     *
     * 집계 결과:
     * | product_id | avg_rating |
     * |------------|------------|
     * | 123        | 4.67       | (5 + 4 + 5) / 3
     * | 456        | 3.50       | (3 + 4) / 2
     *
     * 【 반환 타입 】
     * - Dictionary<long, double>: Key=product_id, Value=avg_rating
     * - long: 상품 ID (BIGINT)
     * - double: 평균 평점 (소수점 포함)
     *
     * 【 Dictionary<TKey, TValue> 개념 】
     * - Key-Value 쌍의 컬렉션
     * - Key로 빠른 조회 (O(1))
     * - 중복 Key 불가
     *
     * 【 예상 성능 】
     * - 응답 시간: 50-200ms (500만 건 스캔)
     * - 메모리 할당: 100-200 KB (상품 5만 개 × 16 bytes)
     * - GC Gen0: 10-20회
     *
     * 【 NuVatis XML 매퍼 예시 】
     * <select id="GetAverageRatingByProductAsync" resultType="map">
     *   SELECT product_id AS key,
     *          AVG(rating) AS value
     *   FROM reviews
     *   GROUP BY product_id
     *   ORDER BY value DESC
     * </select>
     */
    [Benchmark(Description = "C01_Aggregate_AVG_NuVatis")]
    public async Task<Dictionary<long, double>> C01_NuVatis() =>
        await _reviewNuvatis.GetAverageRatingByProductAsync();

    /**
     * C01: 상품별 평균 평점 집계 - Dapper 구현
     *
     * 【 Dapper 코드 예시 】
     * var sql = @"
     *     SELECT product_id, AVG(rating) AS avg_rating
     *     FROM reviews
     *     GROUP BY product_id
     *     ORDER BY avg_rating DESC
     * ";
     *
     * var results = await connection.QueryAsync<(long productId, double avgRating)>(sql);
     *
     * var dict = results.ToDictionary(
     *     r => r.productId,
     *     r => r.avgRating
     * );
     *
     * return dict;
     *
     * 【 Dapper Tuple 매핑 】
     * - (long productId, double avgRating): Tuple 타입
     * - SQL 컬럼명과 Tuple 필드명 자동 매핑
     * - product_id → productId
     * - avg_rating → avgRating
     *
     * 【 ToDictionary() LINQ 메서드 】
     * - IEnumerable<T>를 Dictionary<TKey, TValue>로 변환
     * - 첫 번째 람다: Key 선택 (productId)
     * - 두 번째 람다: Value 선택 (avgRating)
     *
     * 【 Dapper 장점 (집계 쿼리) 】
     * - 빠른 성능 (네이티브에 가까움)
     * - 낮은 메모리 사용량
     * - 간단한 SQL 직접 작성
     */
    [Benchmark(Description = "C01_Aggregate_AVG_Dapper")]
    public async Task<Dictionary<long, double>> C01_Dapper() =>
        await _reviewDapper.GetAverageRatingByProductAsync();

    /**
     * C01: 상품별 평균 평점 집계 - EF Core 구현
     *
     * 【 EF Core LINQ 쿼리 】
     * var results = await context.Reviews
     *     .GroupBy(r => r.ProductId)
     *     .Select(g => new
     *     {
     *         ProductId = g.Key,
     *         AvgRating = g.Average(r => r.Rating)
     *     })
     *     .OrderByDescending(x => x.AvgRating)
     *     .ToDictionaryAsync(x => x.ProductId, x => x.AvgRating);
     *
     * return results;
     *
     * 【 LINQ 메서드 체이닝 】
     * - GroupBy(r => r.ProductId): product_id로 그룹화
     * - Select(g => new { ... }): 익명 타입으로 투영
     * - g.Key: 그룹 키 (product_id)
     * - g.Average(r => r.Rating): 그룹 평균
     * - OrderByDescending(): 내림차순 정렬
     * - ToDictionaryAsync(): Dictionary 변환
     *
     * 【 익명 타입 (Anonymous Type) 】
     * - new { ProductId = ..., AvgRating = ... }
     * - 컴파일 타임에 자동 생성되는 타입
     * - 읽기 전용 프로퍼티
     * - 타입명 없음 (var로 선언)
     *
     * 【 EF Core SQL 번역 】
     * LINQ → SQL 자동 변환:
     *   GroupBy(r => r.ProductId)
     *   → GROUP BY product_id
     *
     *   g.Average(r => r.Rating)
     *   → AVG(rating)
     *
     *   OrderByDescending(x => x.AvgRating)
     *   → ORDER BY avg_rating DESC
     *
     * 【 EF Core 단점 (집계 쿼리) 】
     * - 복잡한 LINQ → 비효율적 SQL 생성 가능
     * - Change Tracking 비용 (AsNoTracking() 권장)
     * - 상대적으로 느린 성능
     */
    [Benchmark(Description = "C01_Aggregate_AVG_EfCore")]
    public async Task<Dictionary<long, double>> C01_EfCore() =>
        await _reviewEfCore.GetAverageRatingByProductAsync();

    // ========================================
    // C06-C10: Window Functions
    // ========================================

    /**
     * C06: 상품별 상위 N개 리뷰 조회 - NuVatis 구현
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
     * 【 Window Function 실행 과정 】
     * 1. reviews, users, products JOIN
     * 2. PARTITION BY product_id: 상품별 그룹 분할
     * 3. ORDER BY rating DESC: 평점 높은 순 정렬
     * 4. ROW_NUMBER(): 각 그룹 내 순위 부여
     * 5. WHERE rank <= 10: 상위 10개만 필터링
     *
     * 【 ROW_NUMBER() 개념 】
     * - 각 파티션(그룹) 내에서 순차적 번호 부여
     * - 동점이어도 다른 순위 (1, 2, 3, ...)
     * - ORDER BY 기준으로 정렬 후 번호 할당
     *
     * 【 PARTITION BY 개념 】
     * - 데이터를 그룹으로 분할
     * - 각 그룹마다 독립적인 Window Function 적용
     * - 예: product_id별로 분할 → 각 상품마다 별도 순위
     *
     * 【 예시 데이터 】
     * reviews 테이블:
     * | product_id | rating | created_at |
     * |------------|--------|------------|
     * | 123        | 5      | 2026-03-01 |
     * | 123        | 4      | 2026-02-28 |
     * | 123        | 5      | 2026-02-25 |
     * | 456        | 5      | 2026-03-01 |
     * | 456        | 3      | 2026-02-28 |
     *
     * Window Function 결과:
     * | product_id | rating | rank |
     * |------------|--------|------|
     * | 123        | 5      | 1    | ← 최신 5점
     * | 123        | 5      | 2    | ← 과거 5점
     * | 123        | 4      | 3    |
     * | 456        | 5      | 1    |
     * | 456        | 3      | 2    |
     *
     * 【 반환 타입 】
     * - Task<IEnumerable<dynamic>>: 동적 타입 컬렉션
     * - dynamic: 런타임에 타입 결정 (컴파일 타임 체크 없음)
     *
     * 【 dynamic 타입 사용 이유 】
     * - Window Function 결과에 rank 컬럼 추가
     * - 기존 Review 모델에 없는 프로퍼티
     * - 익명 타입 또는 dynamic으로 처리
     *
     * 【 예상 성능 】
     * - 응답 시간: 10-40ms (정렬 + 순위 계산)
     * - 메모리 할당: 50-100 KB
     */
    [Benchmark(Description = "C06_Window_Functions_NuVatis")]
    public async Task<IEnumerable<dynamic>> C06_NuVatis() =>
        await _reviewNuvatis.GetTopReviewsByProductAsync(10);

    /**
     * C06: 상품별 상위 N개 리뷰 조회 - Dapper 구현
     *
     * 【 Dapper dynamic 매핑 】
     * - SQL 결과를 dynamic 객체로 자동 매핑
     * - 컬럼명이 프로퍼티명이 됨
     *
     * 【 Dapper 코드 예시 】
     * var sql = @"
     *     SELECT r.*, u.user_name, p.product_name,
     *            ROW_NUMBER() OVER (PARTITION BY r.product_id ORDER BY r.rating DESC) AS rank
     *     FROM reviews r
     *     JOIN users u ON r.user_id = u.id
     *     JOIN products p ON r.product_id = p.id
     * ";
     *
     * var results = await connection.QueryAsync<dynamic>(sql);
     *
     * var filtered = results
     *     .Where(r => r.rank <= topN)
     *     .OrderBy(r => r.product_id)
     *     .ThenBy(r => r.rank);
     *
     * return filtered;
     *
     * 【 Dapper QueryAsync<dynamic>() 】
     * - 타입을 dynamic으로 지정
     * - SQL 컬럼이 프로퍼티가 됨
     * - r.rank, r.rating, r.user_name 등 접근 가능
     */
    [Benchmark(Description = "C06_Window_Functions_Dapper")]
    public async Task<IEnumerable<dynamic>> C06_Dapper() =>
        await _reviewDapper.GetTopReviewsByProductAsync(10);

    /**
     * C06: 상품별 상위 N개 리뷰 조회 - EF Core 구현
     *
     * 【 EF Core Window Functions 제한 】
     * - EF Core 6.0 이전: Window Functions 미지원
     * - EF Core 6.0+: 제한적 지원 (ROW_NUMBER만)
     * - 복잡한 Window Functions: Raw SQL 사용 권장
     *
     * 【 EF Core Raw SQL 예시 】
     * var sql = @"
     *     SELECT r.*, u.user_name, p.product_name,
     *            ROW_NUMBER() OVER (PARTITION BY r.product_id ORDER BY r.rating DESC) AS rank
     *     FROM reviews r
     *     JOIN users u ON r.user_id = u.id
     *     JOIN products p ON r.product_id = p.id
     * ";
     *
     * var results = await context.Reviews
     *     .FromSqlRaw(sql)
     *     .ToListAsync();
     *
     * 【 EF Core 대안: 클라이언트 측 처리 】
     * var reviews = await context.Reviews
     *     .Include(r => r.User)
     *     .Include(r => r.Product)
     *     .ToListAsync();
     *
     * var ranked = reviews
     *     .GroupBy(r => r.ProductId)
     *     .SelectMany(g => g
     *         .OrderByDescending(r => r.Rating)
     *         .ThenByDescending(r => r.CreatedAt)
     *         .Take(topN)
     *     );
     *
     * [위험] 전체 데이터 로드 후 메모리에서 처리
     * → 메모리 사용량 증가
     * → 성능 저하 (500만 건 로드 불가능)
     */
    [Benchmark(Description = "C06_Window_Functions_EfCore")]
    public async Task<IEnumerable<dynamic>> C06_EfCore() =>
        await _reviewEfCore.GetTopReviewsByProductAsync(10);

    // ========================================
    // C11-C15: JOIN + GROUP BY
    // ========================================

    /**
     * C11: 국가별 사용자 수 집계 - NuVatis 구현
     *
     * 【 SQL 쿼리 】
     * SELECT a.country,
     *        COUNT(DISTINCT u.id) AS user_count
     * FROM users u
     * JOIN addresses a ON u.id = a.user_id
     * WHERE a.is_default = TRUE
     * GROUP BY a.country
     * ORDER BY user_count DESC;
     *
     * 【 JOIN + GROUP BY 조합 】
     * 1. users와 addresses JOIN
     * 2. is_default = TRUE로 필터링 (기본 주소만)
     * 3. country별로 그룹화
     * 4. 각 그룹의 사용자 수 집계 (COUNT DISTINCT)
     *
     * 【 COUNT(DISTINCT u.id) 개념 】
     * - DISTINCT: 중복 제거
     * - 한 사용자가 여러 국가에 주소 가질 경우 중복 방지
     * - COUNT(*): 전체 행 수
     * - COUNT(DISTINCT): 고유 값 개수
     *
     * 【 예시 데이터 】
     * users + addresses (JOIN 후):
     * | user_id | country   | is_default |
     * |---------|-----------|------------|
     * | 1       | Korea     | TRUE       |
     * | 2       | Korea     | TRUE       |
     * | 3       | USA       | TRUE       |
     * | 3       | Korea     | FALSE      | ← is_default = FALSE (제외)
     *
     * 집계 결과:
     * | country | user_count |
     * |---------|------------|
     * | Korea   | 2          |
     * | USA     | 1          |
     *
     * 【 반환 타입 】
     * - Dictionary<string, int>: Key=country, Value=user_count
     * - string: 국가명
     * - int: 사용자 수
     *
     * 【 예상 성능 】
     * - 응답 시간: 100-300ms (JOIN + GROUP BY)
     * - 메모리 할당: 10-20 KB (국가 수 적음)
     */
    [Benchmark(Description = "C11_JOIN_GROUP_BY_NuVatis")]
    public async Task<Dictionary<string, int>> C11_NuVatis() =>
        await _userNuvatis.GetUserCountByCountryAsync();

    /**
     * C11: 국가별 사용자 수 집계 - Dapper 구현
     *
     * 【 Dapper 코드 예시 】
     * var sql = @"
     *     SELECT a.country, COUNT(DISTINCT u.id) AS user_count
     *     FROM users u
     *     JOIN addresses a ON u.id = a.user_id
     *     WHERE a.is_default = TRUE
     *     GROUP BY a.country
     *     ORDER BY user_count DESC
     * ";
     *
     * var results = await connection.QueryAsync<(string country, int userCount)>(sql);
     *
     * return results.ToDictionary(r => r.country, r => r.userCount);
     *
     * 【 Dapper 장점 (복잡한 쿼리) 】
     * - SQL을 있는 그대로 작성
     * - 명확하고 직관적
     * - 최적화된 쿼리 작성 용이
     */
    [Benchmark(Description = "C11_JOIN_GROUP_BY_Dapper")]
    public async Task<Dictionary<string, int>> C11_Dapper() =>
        await _userDapper.GetUserCountByCountryAsync();

    /**
     * C11: 국가별 사용자 수 집계 - EF Core 구현
     *
     * 【 EF Core LINQ 쿼리 】
     * var results = await context.Users
     *     .Join(
     *         context.Addresses.Where(a => a.IsDefault),
     *         u => u.Id,
     *         a => a.UserId,
     *         (u, a) => new { u.Id, a.Country }
     *     )
     *     .GroupBy(x => x.Country)
     *     .Select(g => new
     *     {
     *         Country = g.Key,
     *         UserCount = g.Select(x => x.Id).Distinct().Count()
     *     })
     *     .OrderByDescending(x => x.UserCount)
     *     .ToDictionaryAsync(x => x.Country, x => x.UserCount);
     *
     * 【 LINQ Join() 메서드 】
     * - Join(내부 컬렉션, 외부 키 선택, 내부 키 선택, 결과 선택)
     * - SQL INNER JOIN으로 번역
     *
     * 【 EF Core 복잡한 LINQ 단점 】
     * - 가독성 저하
     * - 비효율적 SQL 생성 가능
     * - 복잡한 쿼리는 Raw SQL 권장
     */
    [Benchmark(Description = "C11_JOIN_GROUP_BY_EfCore")]
    public async Task<Dictionary<string, int>> C11_EfCore() =>
        await _userEfCore.GetUserCountByCountryAsync();
}
