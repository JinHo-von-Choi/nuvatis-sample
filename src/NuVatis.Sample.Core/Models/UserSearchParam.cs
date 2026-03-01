/*
 * ==================================================================================
 * UserSearchParam - NuVatis 동적 SQL 파라미터 객체
 * ==================================================================================
 *
 * 이 클래스는 NuVatis 동적 쿼리(<where>, <if>, <foreach>)에 사용되는
 * 검색 조건 파라미터를 담는 객체입니다.
 *
 * ==================================================================================
 * 동적 SQL 매핑
 * ==================================================================================
 *
 * IUserMapper.xml의 Search 쿼리에서 사용:
 *
 *     <select id="Search" resultMap="UserResult">
 *       SELECT * FROM users
 *       <where>
 *         <if test="UserName != null">
 *           AND user_name LIKE '%' || #{UserName} || '%'
 *         </if>
 *         <if test="Email != null">
 *           AND email LIKE '%' || #{Email} || '%'
 *         </if>
 *         <if test="IsActive != null">
 *           AND is_active = #{IsActive}
 *         </if>
 *         <if test="Ids != null and Ids.Count > 0">
 *           AND id IN
 *           <foreach collection="Ids" item="id" open="(" separator="," close=")">
 *             #{id}
 *           </foreach>
 *         </if>
 *       </where>
 *       <if test="Offset != null">OFFSET #{Offset}</if>
 *       <if test="Limit != null">LIMIT #{Limit}</if>
 *     </select>
 *
 * ==================================================================================
 * 속성 설명
 * ==================================================================================
 *
 * UserName (string?):
 *  - 사용자명 부분 일치 검색
 *  - NULL이면 조건 무시
 *  - SQL: LIKE '%{UserName}%'
 *  - 예: "john" → "john_doe", "johnny" 모두 매칭
 *
 * Email (string?):
 *  - 이메일 부분 일치 검색
 *  - NULL이면 조건 무시
 *  - SQL: LIKE '%{Email}%'
 *  - 예: "example" → "john@example.com" 매칭
 *
 * IsActive (bool?):
 *  - 활성 여부 필터
 *  - NULL이면 조건 무시 (전체 조회)
 *  - true: 활성 사용자만
 *  - false: 비활성 사용자만
 *
 * Ids (List<int>?):
 *  - ID 목록 검색 (IN 절)
 *  - NULL 또는 빈 리스트면 조건 무시
 *  - SQL: IN (1, 2, 3, ...)
 *  - 예: [1, 2, 3] → id IN (1, 2, 3)
 *
 * Offset (int?):
 *  - 페이징 시작 위치
 *  - NULL이면 OFFSET 절 무시 (처음부터)
 *  - 0: 첫 페이지
 *  - 10: 11번째 레코드부터
 *
 * Limit (int?):
 *  - 페이징 크기 (페이지당 레코드 수)
 *  - NULL이면 LIMIT 절 무시 (전체 조회)
 *  - 10: 10개씩
 *  - 100: 100개씩
 *
 * ==================================================================================
 * 사용 예제
 * ==================================================================================
 *
 * 1. 모든 사용자 조회:
 *     var param = new UserSearchParam();
 *     var users = _userMapper.Search(param);
 *
 * 2. 이름으로 검색:
 *     var param = new UserSearchParam { UserName = "john" };
 *     var users = _userMapper.Search(param);
 *
 * 3. 활성 사용자만:
 *     var param = new UserSearchParam { IsActive = true };
 *     var users = _userMapper.Search(param);
 *
 * 4. 복합 검색 + 페이징:
 *     var param = new UserSearchParam
 *     {
 *         UserName = "john",
 *         Email = "example",
 *         IsActive = true,
 *         Offset = 0,
 *         Limit = 10
 *     };
 *     var users = _userMapper.Search(param);
 *
 * 5. 특정 ID 목록:
 *     var param = new UserSearchParam
 *     {
 *         Ids = new List<int> { 1, 2, 3 }
 *     };
 *     var users = _userMapper.Search(param);
 *
 * ==================================================================================
 * nullable 타입의 중요성
 * ==================================================================================
 *
 * 모든 속성이 nullable (?, NULL 허용):
 *  - 선택적 파라미터 구현
 *  - NULL = 조건 무시
 *  - NOT NULL = 조건 적용
 *
 * 예시:
 *     // IsActive = null → WHERE 절에 is_active 조건 없음
 *     var param = new UserSearchParam { UserName = "john" };
 *
 *     // IsActive = true → WHERE 절에 is_active = true 추가
 *     var param = new UserSearchParam { UserName = "john", IsActive = true };
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
 * 사용자 검색 파라미터 (NuVatis 동적 SQL용)
 * 모든 속성 nullable - NULL이면 조건 무시
 */
public class UserSearchParam
{
    /** 사용자명 부분 일치 검색 (LIKE '%{UserName}%') */
    public string? UserName { get; set; }

    /** 이메일 부분 일치 검색 (LIKE '%{Email}%') */
    public string? Email { get; set; }

    /** 활성 여부 필터 (NULL: 전체, true: 활성, false: 비활성) */
    public bool? IsActive { get; set; }

    /** ID 목록 검색 (IN 절, NULL 또는 빈 리스트: 조건 무시) */
    public List<int>? Ids { get; set; }

    /** 페이징 시작 위치 (NULL: OFFSET 없음, 0: 첫 페이지) */
    public int? Offset { get; set; }

    /** 페이징 크기 (NULL: LIMIT 없음, 10: 10개씩) */
    public int? Limit { get; set; }
}
