namespace NuVatis.Benchmark.Core.Models;

/**
 * 카테고리 엔티티 (Entity) - 계층형 구조 (Self-Referencing)
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - 계층형 구조: 부모-자식 관계를 자기 자신과 맺음 (트리 구조)
 *
 * 【 테이블 정보 】
 * - 테이블명: categories
 * - 레코드 수: 500개 (카테고리 트리)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: parent_id → categories.id (Self-Referencing, 자기 참조)
 *
 * 【 계층형 구조 예시 】
 * - 전자제품 (parent_id = NULL) ← 최상위 카테고리
 *   ├─ 컴퓨터 (parent_id = 1)
 *   │  ├─ 노트북 (parent_id = 2)
 *   │  └─ 데스크톱 (parent_id = 2)
 *   └─ 스마트폰 (parent_id = 1)
 *      ├─ 삼성 (parent_id = 5)
 *      └─ 애플 (parent_id = 5)
 *
 * 【 실전 활용 】
 * - 상품 분류: 대분류 → 중분류 → 소분류
 * - 메뉴 구조: 헤더 메뉴, 서브 메뉴
 * - 조직도: 본부 → 팀 → 파트
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Category
{
    /**
     * 카테고리 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가 (1, 2, 3, ...)
     */
    public long Id { get; set; }

    /**
     * 부모 카테고리 ID (Foreign Key, Self-Referencing)
     *
     * 【 SQL 매핑 】
     * - 컬럼: parent_id BIGINT NULL
     * - FK: FOREIGN KEY (parent_id) REFERENCES categories(id)
     * - INDEX: idx_categories_parent_id (자식 카테고리 조회 최적화)
     *
     * 【 .NET Nullable 개념 】
     * - long?: Nullable<long> (값이 없을 수 있음)
     * - null: 최상위 카테고리 (루트, Root)
     * - 값 있음: 하위 카테고리 (부모 카테고리 ID)
     *
     * 【 계층 구조 예시 】
     * | id | parent_id | category_name |
     * |----|-----------|---------------|
     * | 1  | NULL      | 전자제품      | ← 최상위
     * | 2  | 1         | 컴퓨터        | ← 1의 자식
     * | 3  | 2         | 노트북        | ← 2의 자식
     * | 4  | 2         | 데스크톱      | ← 2의 자식
     * | 5  | 1         | 스마트폰      | ← 1의 자식
     *
     * 【 SQL 쿼리 예시 】
     * -- 최상위 카테고리 조회 (ROOT)
     * SELECT * FROM categories
     * WHERE parent_id IS NULL;
     *
     * -- 특정 카테고리의 자식 조회
     * SELECT * FROM categories
     * WHERE parent_id = 1
     * ORDER BY display_order ASC;
     *
     * -- 전체 계층 조회 (Recursive CTE)
     * WITH RECURSIVE category_tree AS (
     *     -- 최상위 카테고리 (루트)
     *     SELECT id, parent_id, category_name, 0 AS level
     *     FROM categories
     *     WHERE parent_id IS NULL
     *
     *     UNION ALL
     *
     *     -- 재귀적으로 자식 카테고리 조회
     *     SELECT c.id, c.parent_id, c.category_name, ct.level + 1
     *     FROM categories c
     *     JOIN category_tree ct ON c.parent_id = ct.id
     * )
     * SELECT * FROM category_tree
     * ORDER BY level, category_name;
     *
     * 【 Recursive CTE (Common Table Expression) 】
     * - 재귀 쿼리: 자기 자신을 참조하는 쿼리
     * - 계층형 데이터 조회에 필수
     * - PostgreSQL, MySQL 8.0+, SQL Server 지원
     *
     * 【 주의사항 】
     * - 순환 참조 방지: A → B → A (무한 루프)
     *   제약: CHECK (id != parent_id)
     * - 최대 깊이 제한: 일반적으로 3-5 레벨 (대-중-소-세-세세)
     */
    public long? ParentId { get; set; }

    /**
     * 카테고리명
     *
     * 【 SQL 매핑 】
     * - 컬럼: category_name VARCHAR(100) NOT NULL
     * - INDEX: idx_categories_name (카테고리 검색)
     *
     * 【 실전 활용 】
     * - 상품 목록 표시: "전자제품 > 컴퓨터 > 노트북"
     * - Breadcrumb (빵 부스러기 네비게이션): Home > 전자제품 > 컴퓨터
     *
     * 【 경로 조회 (Path) 】
     * WITH RECURSIVE category_path AS (
     *     -- 특정 카테고리 (예: 노트북, id=3)
     *     SELECT id, parent_id, category_name, category_name AS path
     *     FROM categories
     *     WHERE id = 3
     *
     *     UNION ALL
     *
     *     -- 부모 카테고리를 재귀적으로 조회
     *     SELECT c.id, c.parent_id, c.category_name,
     *            c.category_name || ' > ' || cp.path AS path
     *     FROM categories c
     *     JOIN category_path cp ON c.id = cp.parent_id
     * )
     * SELECT path FROM category_path
     * WHERE parent_id IS NULL; -- 최상위에 도달하면 종료
     *
     * 결과: "전자제품 > 컴퓨터 > 노트북"
     */
    public string CategoryName { get; set; } = string.Empty;

    /**
     * 카테고리 설명 (Description) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: description TEXT NULL
     *
     * 【 실전 활용 】
     * - SEO: 메타 설명 (Meta Description)
     * - 카테고리 페이지 상단 설명문
     */
    public string? Description { get; set; }

    /**
     * 표시 순서 (Display Order)
     *
     * 【 SQL 매핑 】
     * - 컬럼: display_order INT DEFAULT 0
     *
     * 【 정렬 우선순위 】
     * - display_order ASC, category_name ASC
     * - 작은 숫자가 먼저 표시 (0, 1, 2, ...)
     *
     * 【 실전 활용 】
     * -- 메뉴 표시 순서
     * SELECT * FROM categories
     * WHERE parent_id = 1 AND is_active = TRUE
     * ORDER BY display_order ASC, category_name ASC;
     *
     * 【 관리자 기능 】
     * - 드래그 앤 드롭으로 순서 변경
     * - display_order를 1씩 증가시켜 순서 관리
     *   UPDATE categories SET display_order = 0 WHERE id = 5; -- 최상단
     *   UPDATE categories SET display_order = 1 WHERE id = 3;
     *   UPDATE categories SET display_order = 2 WHERE id = 4;
     */
    public int DisplayOrder { get; set; } = 0;

    /**
     * 활성 상태 (true: 표시, false: 숨김)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_active BOOLEAN DEFAULT TRUE
     *
     * 【 실전 활용 】
     * - 시즌 카테고리: 여름 의류, 겨울 의류 (계절별 활성/비활성)
     * - 이벤트 카테고리: 블랙프라이데이 (기간 한정)
     *
     * 【 소프트 삭제 】
     * - 물리 삭제: DELETE (복구 불가)
     * - 논리 삭제: is_active = FALSE (복구 가능)
     *
     * 【 계층형 비활성화 】
     * -- 부모 카테고리 비활성화 시 모든 자식도 비활성화?
     * [방법 1] 애플리케이션 계층에서 처리
     *   if (!parent.IsActive) {
     *       return EmptyList; // 부모가 비활성이면 자식도 숨김
     *   }
     *
     * [방법 2] Recursive CTE로 계층 확인
     *   WITH RECURSIVE active_categories AS (
     *       SELECT id FROM categories
     *       WHERE parent_id IS NULL AND is_active = TRUE
     *
     *       UNION ALL
     *
     *       SELECT c.id FROM categories c
     *       JOIN active_categories ac ON c.parent_id = ac.id
     *       WHERE c.is_active = TRUE
     *   )
     *   SELECT * FROM active_categories;
     */
    public bool IsActive { get; set; } = true;

    /**
     * 생성 일시 (카테고리 생성 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (카테고리 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 부모 카테고리 (N:1 관계, Self-Referencing)
     *
     * 【 관계 】
     * - categories (N) → categories (1)
     * - 여러 자식 카테고리가 한 부모 카테고리에 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT c.*, parent.category_name AS parent_name
     * FROM categories c
     * LEFT JOIN categories parent ON c.parent_id = parent.id
     * WHERE c.id = 3;
     *
     * 결과:
     * | id | category_name | parent_name |
     * |----|---------------|-------------|
     * | 3  | 노트북        | 컴퓨터      |
     *
     * 【 .NET 사용 예시 】
     * Category category = await repository.GetByIdAsync(3);
     * Console.WriteLine($"부모: {category.Parent?.CategoryName}"); // "컴퓨터"
     */
    public Category? Parent { get; set; }

    /**
     * 자식 카테고리 목록 (1:N 관계, Self-Referencing)
     *
     * 【 관계 】
     * - categories (1) ↔ categories (N)
     * - 한 카테고리는 여러 자식 카테고리를 가질 수 있음
     *
     * 【 SQL 예시 】
     * -- 특정 카테고리의 자식 목록
     * SELECT * FROM categories
     * WHERE parent_id = 1
     * ORDER BY display_order ASC;
     *
     * 【 .NET 사용 예시 】
     * Category category = await repository.GetByIdAsync(1);
     * foreach (var child in category.Children ?? Enumerable.Empty<Category>())
     * {
     *     Console.WriteLine($"자식: {child.CategoryName}");
     * }
     *
     * 【 재귀적 로드 (Recursive Loading) 】
     * -- EF Core에서는 Include()로 1레벨만 로드 가능
     * var category = await context.Categories
     *     .Include(c => c.Children)
     *         .ThenInclude(c => c.Children) // 2레벨
     *             .ThenInclude(c => c.Children) // 3레벨
     *     .FirstOrDefaultAsync(c => c.Id == 1);
     *
     * -- 전체 트리 로드는 Recursive CTE 또는 애플리케이션 계층에서 처리
     *
     * 【 N+1 문제 주의 】
     * [비권장] Lazy Loading:
     *   foreach (var category in categories) {
     *       foreach (var child in category.Children) { // 매번 DB 쿼리!
     *           ...
     *       }
     *   }
     *   → 카테고리 100개면 101번 쿼리 (1 + 100)
     *
     * [권장] Eager Loading:
     *   var categories = context.Categories
     *       .Include(c => c.Children)
     *       .ToList();
     *   → 1번 쿼리 (JOIN)
     */
    public ICollection<Category>? Children { get; set; }

    /**
     * 카테고리에 속한 상품 목록 (1:N 관계)
     *
     * 【 관계 】
     * - categories (1) ↔ products (N)
     * - 한 카테고리는 여러 상품을 가질 수 있음
     *
     * 【 SQL 예시 】
     * -- 특정 카테고리의 상품 목록
     * SELECT p.*
     * FROM products p
     * WHERE p.category_id = 3
     *   AND p.is_active = TRUE
     * ORDER BY p.created_at DESC;
     *
     * -- 카테고리별 상품 수
     * SELECT c.category_name, COUNT(p.id) AS product_count
     * FROM categories c
     * LEFT JOIN products p ON c.id = p.category_id AND p.is_active = TRUE
     * GROUP BY c.id, c.category_name
     * ORDER BY product_count DESC;
     *
     * 【 .NET 사용 예시 】
     * Category category = await repository.GetByIdAsync(3);
     * foreach (var product in category.Products ?? Enumerable.Empty<Product>())
     * {
     *     Console.WriteLine($"상품: {product.ProductName}");
     * }
     *
     * 【 하위 카테고리 상품 포함 조회 】
     * -- "컴퓨터" 카테고리의 모든 상품 (노트북 + 데스크톱 포함)
     * WITH RECURSIVE sub_categories AS (
     *     SELECT id FROM categories WHERE id = 2
     *
     *     UNION ALL
     *
     *     SELECT c.id FROM categories c
     *     JOIN sub_categories sc ON c.parent_id = sc.id
     * )
     * SELECT p.*
     * FROM products p
     * WHERE p.category_id IN (SELECT id FROM sub_categories)
     *   AND p.is_active = TRUE;
     */
    public ICollection<Product>? Products { get; set; }
}
