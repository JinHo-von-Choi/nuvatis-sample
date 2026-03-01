using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Core.Interfaces;

/**
 * 카테고리 Repository 인터페이스 - 계층형 카테고리 데이터 접근 계층
 *
 * 【 Repository 패턴 】
 * - 데이터 접근 로직을 캡슐화하여 비즈니스 로직과 분리
 * - 인터페이스로 추상화 → ORM 교체 용이 (NuVatis ↔ Dapper ↔ EF Core)
 * - 테스트 용이성 향상 (Mock Repository 사용)
 *
 * 【 계층형 데이터 구조 】
 * - 자기 참조 관계: parent_id → categories.id
 * - 트리 구조: 루트 → 자식 → 손자 → ...
 * - 깊이: 최대 5레벨 (전자제품 → 컴퓨터 → 노트북 → 게이밍 → 고성능)
 *
 * 【 벤치마크 시나리오 】
 * - Simple: GetById, InsertSingle
 * - Complex: RecursiveCTE (계층 조회)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public interface ICategoryRepository
{
    // ========================================
    // Simple: 단순 조회
    // ========================================

    /**
     * 카테고리 ID로 단건 조회 (Primary Key 조회)
     *
     * 【 SQL 쿼리 】
     * SELECT * FROM categories WHERE id = @id;
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1) - Primary Key Index 사용
     * - 예상 응답 시간: <1ms
     * - 인덱스: PRIMARY KEY (id)
     *
     * 【 사용 예시 】
     * Category? category = await repository.GetByIdAsync(123);
     * if (category == null)
     *     throw new NotFoundException("카테고리를 찾을 수 없습니다");
     *
     * Console.WriteLine($"카테고리명: {category.CategoryName}");
     * Console.WriteLine($"부모 ID: {category.ParentId?.ToString() ?? "없음 (루트)"}");
     * Console.WriteLine($"깊이: {category.Depth}");
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - GetById
     */
    Task<Category?> GetByIdAsync(long id);

    // ========================================
    // Complex: 재귀 쿼리 (Recursive CTE)
    // ========================================

    /**
     * 계층형 카테고리 트리 조회 (Recursive CTE)
     *
     * 【 SQL 쿼리 (Recursive CTE) 】
     * WITH RECURSIVE category_tree AS (
     *   -- Anchor Member: 시작점 (루트 카테고리)
     *   SELECT id, category_name, parent_id, depth, 0 AS level
     *   FROM categories
     *   WHERE id = @rootId
     *
     *   UNION ALL
     *
     *   -- Recursive Member: 재귀 부분 (자식 카테고리)
     *   SELECT c.id, c.category_name, c.parent_id, c.depth, ct.level + 1
     *   FROM categories c
     *   INNER JOIN category_tree ct ON c.parent_id = ct.id
     * )
     * SELECT * FROM category_tree
     * ORDER BY level, id;
     *
     * 【 CTE (Common Table Expression) 개념 】
     * - WITH 절로 정의하는 임시 결과 집합
     * - 쿼리 내에서 재사용 가능한 가상 테이블
     * - 복잡한 쿼리를 단계별로 구조화
     *
     * 【 Recursive CTE 구조 】
     * - Anchor Member: 재귀의 시작점 (루트 카테고리)
     * - UNION ALL: Anchor와 Recursive 결과 결합
     * - Recursive Member: 이전 결과를 참조하여 다음 레벨 조회
     * - 종료 조건: 더 이상 자식이 없을 때 자동 종료
     *
     * 【 실행 순서 】
     * 1. Anchor Member 실행 → 루트 카테고리 (id=1, level=0)
     * 2. Recursive Member 실행 → 자식 카테고리 (id=10,11,12, level=1)
     * 3. Recursive Member 재실행 → 손자 카테고리 (id=100,101, level=2)
     * 4. 더 이상 자식 없음 → 종료
     *
     * 【 예시 데이터 】
     * categories 테이블:
     * | id  | category_name | parent_id | depth |
     * |-----|---------------|-----------|-------|
     * | 1   | 전자제품        | NULL      | 0     |
     * | 10  | 컴퓨터         | 1         | 1     |
     * | 11  | 스마트폰       | 1         | 1     |
     * | 100 | 노트북         | 10        | 2     |
     * | 101 | 데스크탑       | 10        | 2     |
     *
     * GetCategoryTreeAsync(1) 결과:
     * | id  | category_name | level |
     * |-----|---------------|-------|
     * | 1   | 전자제품       | 0     | ← Anchor
     * | 10  | 컴퓨터         | 1     | ← Recursive 1회
     * | 11  | 스마트폰       | 1     |
     * | 100 | 노트북         | 2     | ← Recursive 2회
     * | 101 | 데스크탑       | 2     |
     *
     * 【 무한 루프 방지 】
     * [위험] 순환 참조 데이터:
     *   A → B → C → A (무한 루프)
     *
     * [안전] 최대 깊이 제한:
     *   WITH RECURSIVE category_tree AS (
     *     SELECT *, 0 AS level FROM categories WHERE id = @rootId
     *     UNION ALL
     *     SELECT c.*, ct.level + 1
     *     FROM categories c
     *     JOIN category_tree ct ON c.parent_id = ct.id
     *     WHERE ct.level < 10  -- 최대 10레벨까지만
     *   )
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(N) - 트리의 모든 노드 방문
     * - 예상 응답 시간: 15-50ms (깊이 5, 노드 100개)
     * - 인덱스: idx_categories_parent (parent_id)
     *
     * 【 사용 예시 】
     * var tree = await repository.GetCategoryTreeAsync(1);
     * foreach (var category in tree)
     * {
     *     string indent = new string(' ', category.level * 2);
     *     Console.WriteLine($"{indent}- {category.category_name} (ID: {category.id})");
     * }
     *
     * 출력 예시:
     * - 전자제품 (ID: 1)
     *   - 컴퓨터 (ID: 10)
     *     - 노트북 (ID: 100)
     *     - 데스크탑 (ID: 101)
     *   - 스마트폰 (ID: 11)
     *
     * 【 실전 활용 】
     * - 전자상거래: 카테고리 메뉴 트리 생성
     * - 파일 시스템: 디렉토리 구조 조회
     * - 조직도: 부서 계층 구조 표시
     * - 댓글 시스템: 대댓글 트리 구성
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - RecursiveCTE
     */
    Task<IEnumerable<Category>> GetCategoryTreeAsync(long rootId);

    /**
     * 모든 자식 카테고리 조회 (Recursive CTE)
     *
     * 【 SQL 쿼리 (Recursive CTE) 】
     * WITH RECURSIVE descendants AS (
     *   -- Anchor Member: 시작점 (부모 카테고리)
     *   SELECT id, category_name, parent_id, depth, 0 AS level
     *   FROM categories
     *   WHERE id = @parentId
     *
     *   UNION ALL
     *
     *   -- Recursive Member: 재귀 부분 (모든 자손)
     *   SELECT c.id, c.category_name, c.parent_id, c.depth, d.level + 1
     *   FROM categories c
     *   INNER JOIN descendants d ON c.parent_id = d.id
     * )
     * SELECT * FROM descendants
     * WHERE id != @parentId  -- 시작 카테고리 제외
     * ORDER BY level, id;
     *
     * 【 GetCategoryTreeAsync vs GetAllDescendantsAsync 차이 】
     * GetCategoryTreeAsync(1):
     *   - 루트부터 시작하여 하위 트리 전체 반환
     *   - 결과에 루트 포함
     *
     * GetAllDescendantsAsync(10):
     *   - 특정 카테고리의 모든 자손만 반환
     *   - 결과에 시작 카테고리 미포함
     *
     * 【 예시 데이터 】
     * categories 테이블:
     * | id  | category_name | parent_id | depth |
     * |-----|---------------|-----------|-------|
     * | 1   | 전자제품       | NULL      | 0     |
     * | 10  | 컴퓨터         | 1         | 1     |
     * | 11  | 스마트폰       | 1         | 1     |
     * | 100 | 노트북         | 10        | 2     |
     * | 101 | 데스크탑       | 10        | 2     |
     * | 102 | 태블릿         | 11        | 2     |
     *
     * GetAllDescendantsAsync(10) 결과:
     * | id  | category_name | level |
     * |-----|---------------|-------|
     * | 100 | 노트북         | 1     |
     * | 101 | 데스크탑       | 1     |
     *
     * GetAllDescendantsAsync(1) 결과:
     * | id  | category_name | level |
     * |-----|---------------|-------|
     * | 10  | 컴퓨터         | 1     |
     * | 11  | 스마트폰       | 1     |
     * | 100 | 노트북         | 2     |
     * | 101 | 데스크탑       | 2     |
     * | 102 | 태블릿         | 2     |
     *
     * 【 Recursive CTE vs Self-Join 비교 】
     * [비권장] Self-Join (깊이 고정):
     *   -- 2단계까지만 조회 가능
     *   SELECT c1.*, c2.*, c3.*
     *   FROM categories c1
     *   LEFT JOIN categories c2 ON c1.id = c2.parent_id
     *   LEFT JOIN categories c3 ON c2.id = c3.parent_id
     *   WHERE c1.id = @parentId;
     *
     *   문제점:
     *   - 깊이가 3단계 이상이면 JOIN 추가 필요
     *   - 가변 깊이 처리 불가능
     *   - SQL 복잡도 증가
     *
     * [권장] Recursive CTE:
     *   - 깊이 제한 없음 (자동 재귀)
     *   - 가변 깊이 처리 가능
     *   - SQL 간결
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(N) - 모든 자손 노드 방문
     * - 예상 응답 시간: 15-50ms (깊이 5, 노드 100개)
     * - 인덱스: idx_categories_parent (parent_id)
     *
     * 【 사용 예시 】
     * // "컴퓨터" 카테고리의 모든 하위 카테고리 조회
     * var descendants = await repository.GetAllDescendantsAsync(10);
     * Console.WriteLine("컴퓨터 하위 카테고리:");
     * foreach (var category in descendants)
     * {
     *     Console.WriteLine($"  - {category.CategoryName} (깊이: {category.Depth})");
     * }
     *
     * 출력 예시:
     * 컴퓨터 하위 카테고리:
     *   - 노트북 (깊이: 2)
     *   - 데스크탑 (깊이: 2)
     *
     * 【 실전 활용 】
     * - 카테고리 삭제 시 모든 하위 카테고리도 함께 삭제
     * - 특정 카테고리 하위의 모든 상품 조회
     * - 권한 관리: 부서 권한 부여 시 하위 부서에도 자동 적용
     * - 파일 시스템: 디렉토리 삭제 시 하위 파일/폴더 모두 삭제
     *
     * 【 CASCADE DELETE 예시 】
     * -- 카테고리 삭제 시 모든 자손도 함께 삭제
     * WITH RECURSIVE descendants AS (
     *   SELECT id FROM categories WHERE id = @parentId
     *   UNION ALL
     *   SELECT c.id
     *   FROM categories c
     *   JOIN descendants d ON c.parent_id = d.id
     * )
     * DELETE FROM categories
     * WHERE id IN (SELECT id FROM descendants);
     *
     * 【 벤치마크 시나리오 】
     * - CategoryC: Complex Query - RecursiveCTE
     */
    Task<IEnumerable<Category>> GetAllDescendantsAsync(long parentId);

    // ========================================
    // Simple: 삽입
    // ========================================

    /**
     * 카테고리 단건 삽입 (INSERT)
     *
     * 【 SQL 쿼리 】
     * INSERT INTO categories (category_name, parent_id, depth, created_at)
     * VALUES (@categoryName, @parentId, @depth, @createdAt)
     * RETURNING id;
     *
     * 【 계층형 데이터 삽입 시 주의사항 】
     * - parent_id: 부모 카테고리 ID (NULL이면 루트 카테고리)
     * - depth: 깊이 (루트=0, 자식=1, 손자=2, ...)
     * - 부모 카테고리가 존재하는지 검증 필요
     *
     * 【 depth 계산 방법 】
     * -- 루트 카테고리 (parent_id = NULL)
     * INSERT INTO categories (category_name, parent_id, depth, created_at)
     * VALUES ('전자제품', NULL, 0, NOW());
     *
     * -- 자식 카테고리 (부모의 depth + 1)
     * INSERT INTO categories (category_name, parent_id, depth, created_at)
     * SELECT '컴퓨터', 1, depth + 1, NOW()
     * FROM categories
     * WHERE id = 1;
     *
     * 【 데이터 무결성 검증 】
     * [위험] parent_id 검증 없이 삽입:
     *   INSERT INTO categories (category_name, parent_id, depth)
     *   VALUES ('노트북', 999, 2);  -- parent_id=999 존재하지 않음
     *   → Foreign Key 제약 위반 또는 고아 레코드 생성
     *
     * [안전] parent_id 존재 여부 확인:
     *   if (category.ParentId.HasValue)
     *   {
     *       var parent = await GetByIdAsync(category.ParentId.Value);
     *       if (parent == null)
     *           throw new InvalidOperationException("부모 카테고리가 존재하지 않습니다");
     *
     *       category.Depth = parent.Depth + 1;
     *   }
     *   else
     *   {
     *       category.Depth = 0;  // 루트 카테고리
     *   }
     *
     * 【 성능 특성 】
     * - 시간 복잡도: O(1)
     * - 예상 응답 시간: 2-5ms
     *
     * 【 사용 예시 】
     * // 루트 카테고리 생성
     * Category root = new Category
     * {
     *     CategoryName = "전자제품",
     *     ParentId = null,
     *     Depth = 0,
     *     CreatedAt = DateTime.UtcNow
     * };
     * long rootId = await repository.InsertAsync(root);
     *
     * // 자식 카테고리 생성
     * Category child = new Category
     * {
     *     CategoryName = "컴퓨터",
     *     ParentId = rootId,
     *     Depth = 1,
     *     CreatedAt = DateTime.UtcNow
     * };
     * long childId = await repository.InsertAsync(child);
     *
     * // 손자 카테고리 생성
     * Category grandchild = new Category
     * {
     *     CategoryName = "노트북",
     *     ParentId = childId,
     *     Depth = 2,
     *     CreatedAt = DateTime.UtcNow
     * };
     * long grandchildId = await repository.InsertAsync(grandchild);
     *
     * 【 벤치마크 시나리오 】
     * - CategoryA: Simple CRUD - InsertSingle
     */
    Task<long> InsertAsync(Category category);
}
