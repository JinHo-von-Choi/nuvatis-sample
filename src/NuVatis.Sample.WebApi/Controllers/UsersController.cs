/*
 * ==================================================================================
 * NuVatis 사용자 API 컨트롤러 - RESTful API 완전 가이드
 * ==================================================================================
 *
 * 이 컨트롤러는 NuVatis XML 매퍼를 사용한 RESTful API 구현 예제입니다.
 * ASP.NET Core Web API + NuVatis ORM의 통합 패턴을 보여줍니다.
 *
 * ==================================================================================
 * 핵심 개념: NuVatis 매퍼 직접 주입
 * ==================================================================================
 *
 * 기존 ORM과의 차이점:
 *
 * 1. Entity Framework Core:
 *    - DbContext 주입
 *    - Repository 패턴 선택 (또는 DbSet 직접 사용)
 *    - 예: public UsersController(AppDbContext dbContext)
 *
 * 2. Dapper:
 *    - IDbConnection 주입
 *    - SQL 문자열 직접 작성
 *    - 예: public UsersController(IDbConnection connection)
 *
 * 3. NuVatis (이 방식):
 *    - IUserMapper 직접 주입
 *    - XML에 정의된 쿼리 메서드 호출
 *    - 예: public UsersController(IUserMapper userMapper)
 *
 * **장점:**
 * - 타입 안전성: 컴파일 타임에 메서드 존재 여부 확인
 * - 간결한 코드: SQL 문자열 불필요
 * - 명확한 의도: GetById, Search 등 명시적 메서드명
 * - 테스트 용이: IUserMapper 목 객체 생성 가능
 *
 * **NuVatis Source Generator:**
 * - IUserMapper 인터페이스는 개발자가 정의
 * - NuVatis Source Generator가 구현 클래스 자동 생성
 * - 빌드 시 IUserMapper.xml 파싱하여 메서드 구현
 * - 런타임 리플렉션 없음 (성능 우수)
 *
 * ==================================================================================
 * 의존성 주입 (DI) 설정
 * ==================================================================================
 *
 * Program.cs에서 Mapper 등록:
 *     builder.Services.AddScoped<IUserMapper, IUserMapper>();
 *
 * 주의:
 * - AddScoped: 요청당 하나의 인스턴스 (권장)
 * - AddSingleton: 애플리케이션 전체에서 하나 (비권장, DB 연결 문제)
 * - AddTransient: 매번 새 인스턴스 (비효율)
 *
 * NuVatis는 내부적으로 DB 연결 풀 관리:
 * - Mapper 인스턴스는 가볍고 상태 없음
 * - 각 메서드 호출마다 연결 풀에서 연결 획득/반환
 *
 * ==================================================================================
 * RESTful API 설계 원칙
 * ==================================================================================
 *
 * HTTP 메서드 의미:
 * - GET: 조회 (멱등성, 안전성)
 * - POST: 생성 (비멱등성)
 * - PUT: 전체 수정 (멱등성)
 * - PATCH: 부분 수정 (멱등성)
 * - DELETE: 삭제 (멱등성)
 *
 * 상태 코드:
 * - 200 OK: 조회/수정/삭제 성공
 * - 201 Created: 생성 성공
 * - 204 No Content: 성공했지만 응답 본문 없음
 * - 400 Bad Request: 잘못된 요청
 * - 404 Not Found: 리소스 없음
 * - 500 Internal Server Error: 서버 오류
 *
 * URL 설계:
 * - /api/users: 컬렉션 (목록)
 * - /api/users/{id}: 단일 리소스
 * - /api/users/search: 검색 (쿼리 파라미터)
 *
 * ==================================================================================
 * 에러 처리 전략
 * ==================================================================================
 *
 * 이 예제에서는 간단한 에러 처리:
 * - NotFound(): 404 응답
 * - Ok(): 200 응답
 * - CreatedAtAction(): 201 응답 + Location 헤더
 *
 * 실무에서는 Global Exception Handler 사용 권장:
 *     [ApiController] // 자동 모델 검증
 *     public class UsersController : ControllerBase
 *
 * Global Exception Handler 예제:
 *     app.UseExceptionHandler("/error");
 *     app.Map("/error", (HttpContext context) =>
 *     {
 *         var error = context.Features.Get<IExceptionHandlerFeature>();
 *         return Results.Problem(
 *             title: "서버 오류",
 *             detail: error?.Error.Message
 *         );
 *     });
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
 * 사용자 관리 REST API 컨트롤러
 * NuVatis XML 매퍼를 사용한 CRUD 및 검색 API 제공
 */
[ApiController]                     // 자동 모델 검증, 자동 400 응답
[Route("api/[controller]")]         // URL: /api/users
public class UsersController : ControllerBase
{
    /**
     * NuVatis 사용자 매퍼 (DI로 주입됨)
     * IUserMapper.xml에 정의된 쿼리 실행
     * Source Generator가 자동 구현
     */
    private readonly IUserMapper _userMapper;

    /**
     * 생성자 - 의존성 주입
     * ASP.NET Core DI 컨테이너가 자동으로 IUserMapper 구현체 주입
     * Program.cs에서 AddScoped로 등록됨
     */
    public UsersController(IUserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /*
     * ==================================================================================
     * GetAll: 모든 사용자 조회
     * ==================================================================================
     *
     * HTTP 메서드: GET
     * URL: /api/users
     * 응답: 200 OK + User 배열
     *
     * 사용 목적:
     * - 관리자 대시보드 사용자 목록
     * - 사용자 선택 드롭다운
     * - 내부 시스템 데이터 동기화
     *
     * **주의사항:**
     * - 대량 데이터 조회 시 성능 문제 가능
     * - 실무에서는 페이징 필수 (LIMIT/OFFSET)
     * - 10,000명 이상: 이 API 사용 금지 → Search API 사용
     *
     * **성능 분석:**
     * - 사용자 100명: <10ms, 응답 크기 ~50KB
     * - 사용자 1,000명: ~50ms, 응답 크기 ~500KB
     * - 사용자 10,000명: ~500ms, 응답 크기 ~5MB (위험)
     *
     * **개선 방법:**
     * 1. 페이징 추가:
     *    GET /api/users?offset=0&limit=100
     *
     * 2. 필드 선택:
     *    GET /api/users?fields=id,userName,email
     *
     * 3. 캐싱:
     *    [ResponseCache(Duration = 300)] // 5분 캐싱
     *
     * cURL 예제:
     *     curl -X GET http://localhost:5000/api/users
     *
     * 응답 예제:
     *     [
     *       {
     *         "id": 1,
     *         "userName": "john_doe",
     *         "email": "john@example.com",
     *         "fullName": "John Doe",
     *         "isActive": true,
     *         "createdAt": "2026-01-01T00:00:00Z"
     *       },
     *       ...
     *     ]
     *
     * NuVatis 매퍼 호출:
     * - _userMapper.GetAll()
     * - XML: <select id="GetAll">
     * - SQL: SELECT * FROM users WHERE deleted_at IS NULL
     *
     * 반환 타입:
     * - ActionResult<IList<User>>: 컨트롤러 반환 타입
     * - IList<User>: 실제 데이터 타입
     * - ActionResult: 상태 코드 + 헤더 제어 가능
     *
     * ==================================================================================
     */
    [HttpGet]
    public ActionResult<IList<User>> GetAll()
    {
        // NuVatis XML 매퍼 호출 (IUserMapper.xml의 GetAll 쿼리 실행)
        var users = _userMapper.GetAll();

        // 200 OK 응답 + JSON 본문
        return Ok(users);
    }

    /*
     * ==================================================================================
     * GetById: ID로 사용자 조회
     * ==================================================================================
     *
     * HTTP 메서드: GET
     * URL: /api/users/{id}
     * 응답: 200 OK + User 객체 OR 404 Not Found
     *
     * 사용 목적:
     * - 사용자 프로필 조회
     * - 사용자 상세 정보 화면
     * - 주문/리뷰 작성 시 사용자 정보 확인
     *
     * **비동기 처리 (async/await):**
     * - GetByIdAsync: 비동기 메서드
     * - 이유: I/O 바운드 작업 (DB 조회)
     * - 장점: 스레드 블로킹 방지, 높은 동시성
     *
     * **동기 vs 비동기:**
     *
     * 동기 (GetById):
     * - 호출 스레드가 DB 응답까지 대기
     * - 동시 요청 100개 → 스레드 100개 필요
     * - 스레드 풀 고갈 위험
     *
     * 비동기 (GetByIdAsync):
     * - 호출 스레드가 다른 작업 수행 가능
     * - 동시 요청 100개 → 스레드 수십 개로 처리
     * - 확장성 우수
     *
     * ASP.NET Core 권장사항:
     * - I/O 작업은 항상 async/await 사용
     * - CPU 바운드 작업은 동기 사용
     *
     * **404 Not Found 처리:**
     * - 존재하지 않는 ID → 404 응답
     * - message 필드로 에러 메시지 제공
     * - 클라이언트가 적절히 처리 가능
     *
     * **보안 고려사항:**
     * - ID 노출: 순차적 ID는 추측 가능
     * - 해결: UUID 또는 난독화된 ID 사용
     * - 또는 인증/인가로 접근 제어
     *
     * cURL 예제:
     *     curl -X GET http://localhost:5000/api/users/1
     *
     * 성공 응답 (200):
     *     {
     *       "id": 1,
     *       "userName": "john_doe",
     *       "email": "john@example.com",
     *       "fullName": "John Doe",
     *       "isActive": true,
     *       "createdAt": "2026-01-01T00:00:00Z"
     *     }
     *
     * 실패 응답 (404):
     *     {
     *       "message": "사용자를 찾을 수 없습니다."
     *     }
     *
     * NuVatis 매퍼 호출:
     * - _userMapper.GetByIdAsync(id)
     * - XML: <select id="GetByIdAsync">
     * - SQL: SELECT * FROM users WHERE id = #{Id} AND deleted_at IS NULL
     * - 파라미터 바인딩: #{Id} → id (SQL Injection 방지)
     *
     * 성능:
     * - 쿼리 시간: <1ms (PK 인덱스 사용)
     * - 네트워크: ~1KB
     * - 캐싱 가능: 사용자 정보는 자주 변경되지 않음
     *
     * 실무 팁:
     * - 민감한 정보 제거: 비밀번호 해시 등
     * - DTO 사용:
     *     return Ok(new UserDto
     *     {
     *         Id = user.Id,
     *         UserName = user.UserName,
     *         // password 제외
     *     });
     *
     * ==================================================================================
     */
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(int id)
    {
        // 비동기로 사용자 조회 (I/O 바운드 작업)
        var user = await _userMapper.GetByIdAsync(id);

        // 404 Not Found 처리
        if (user == null)
            return NotFound(new { message = "사용자를 찾을 수 없습니다." });

        // 200 OK 응답 + JSON 본문
        return Ok(user);
    }

    /*
     * ==================================================================================
     * Search: 사용자 동적 검색 (NuVatis의 가장 강력한 기능)
     * ==================================================================================
     *
     * HTTP 메서드: GET
     * URL: /api/users/search?userName=john&email=example&isActive=true&offset=0&limit=10
     * 응답: 200 OK + { data, total, offset, limit }
     *
     * 사용 목적:
     * - 관리자 사용자 검색
     * - 필터링 + 페이징
     * - 동적 조건 (선택적 파라미터)
     *
     * **동적 쿼리란?**
     * - 파라미터 유무에 따라 SQL WHERE 절이 동적으로 변경
     * - 예1: userName만 있으면 → WHERE user_name LIKE '%john%'
     * - 예2: userName + email → WHERE user_name LIKE '%john%' AND email LIKE '%example%'
     * - 예3: 모두 null → WHERE 절 없음 (전체 조회)
     *
     * **NuVatis 동적 SQL 태그:**
     * - <where>: WHERE 절 자동 생성 (첫 AND/OR 제거)
     * - <if test="">: 조건부 SQL 추가
     * - <foreach>: 배열/리스트를 IN 절로 변환
     *
     * IUserMapper.xml의 Search 쿼리 구조:
     *     <select id="Search">
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
     *       <if test="Offset != null">
     *         OFFSET #{Offset}
     *       </if>
     *       <if test="Limit != null">
     *         LIMIT #{Limit}
     *       </if>
     *     </select>
     *
     * **[FromQuery] 속성:**
     * - 쿼리 스트링에서 파라미터 바인딩
     * - ?userName=john → userName 파라미터에 "john" 할당
     * - null 허용 (string?, bool?, int?, int[]?)
     * - 파라미터 없으면 null
     *
     * **UserSearchParam 객체:**
     * - 여러 파라미터를 하나의 객체로 묶음
     * - NuVatis 매퍼에 전달
     * - 장점: 파라미터 추가/수정 용이
     *
     * **페이징 (Offset/Limit):**
     * - Offset: 건너뛸 레코드 수 (시작 위치)
     * - Limit: 가져올 레코드 수 (페이지 크기)
     * - 예: offset=20, limit=10 → 21~30번째 레코드
     *
     * 페이징 계산:
     * - 1페이지: offset=0, limit=10
     * - 2페이지: offset=10, limit=10
     * - N페이지: offset=(N-1)*limit, limit=10
     *
     * **총 개수 조회 (Count):**
     * - 검색 결과의 총 개수
     * - 페이징 UI 표시용 (총 몇 페이지?)
     * - WHERE 조건은 동일, SELECT COUNT(*) 실행
     *
     * **응답 구조:**
     *     {
     *       "data": [...],      // 검색 결과 (User 배열)
     *       "total": 100,       // 총 개수 (전체 검색 결과)
     *       "offset": 0,        // 현재 오프셋
     *       "limit": 10         // 현재 리미트
     *     }
     *
     * 클라이언트는 total로 총 페이지 계산:
     *     totalPages = Math.Ceiling(total / limit)
     *
     * cURL 예제:
     *
     * 1. 모든 사용자 (페이징 없음):
     *     curl -X GET "http://localhost:5000/api/users/search"
     *
     * 2. 이름으로 검색:
     *     curl -X GET "http://localhost:5000/api/users/search?userName=john"
     *
     * 3. 이름 + 활성 사용자:
     *     curl -X GET "http://localhost:5000/api/users/search?userName=john&isActive=true"
     *
     * 4. 페이징:
     *     curl -X GET "http://localhost:5000/api/users/search?offset=0&limit=10"
     *
     * 5. 특정 ID 목록:
     *     curl -X GET "http://localhost:5000/api/users/search?ids=1&ids=2&ids=3"
     *
     * 6. 복합 검색:
     *     curl -X GET "http://localhost:5000/api/users/search?userName=john&email=example&isActive=true&offset=0&limit=10"
     *
     * 응답 예제:
     *     {
     *       "data": [
     *         { "id": 1, "userName": "john_doe", ... },
     *         { "id": 2, "userName": "john_smith", ... }
     *       ],
     *       "total": 2,
     *       "offset": 0,
     *       "limit": 10
     *     }
     *
     * **성능 최적화:**
     *
     * 1. 인덱스 필수:
     *     CREATE INDEX idx_users_username ON users(user_name);
     *     CREATE INDEX idx_users_email ON users(email);
     *     CREATE INDEX idx_users_is_active ON users(is_active);
     *
     * 2. LIKE 검색 주의:
     *     LIKE '%john%' → Full Table Scan (느림)
     *     LIKE 'john%' → 인덱스 사용 가능 (빠름)
     *     해결: 전문 검색 엔진 (Elasticsearch) 고려
     *
     * 3. Count 쿼리 최적화:
     *     SELECT COUNT(*) → 느릴 수 있음
     *     캐싱 또는 근사값 사용 고려
     *
     * **보안 주의사항:**
     * - SQL Injection: NuVatis 파라미터 바인딩으로 방지됨
     * - 대량 조회 방지: limit 최대값 제한 (예: 100)
     * - 권한 체크: 일반 사용자는 자신만 조회 가능하도록
     *
     * 실무 팁:
     * - limit 기본값 설정: limit ?? 10
     * - 최대값 제한: Math.Min(limit ?? 10, 100)
     * - 정렬 추가: ORDER BY created_at DESC
     * - 캐싱: 자주 사용되는 검색 조건 캐싱
     *
     * ==================================================================================
     */
    [HttpGet("search")]
    public ActionResult<IList<User>> Search(
        [FromQuery] string? userName,   // 쿼리 스트링: ?userName=john
        [FromQuery] string? email,      // 쿼리 스트링: &email=example
        [FromQuery] bool?   isActive,   // 쿼리 스트링: &isActive=true
        [FromQuery] int[]?  ids,        // 쿼리 스트링: &ids=1&ids=2&ids=3
        [FromQuery] int?    offset,     // 쿼리 스트링: &offset=0
        [FromQuery] int?    limit)      // 쿼리 스트링: &limit=10
    {
        // 파라미터를 객체로 묶음 (NuVatis 매퍼에 전달)
        var param = new UserSearchParam
        {
            UserName = userName,
            Email    = email,
            IsActive = isActive,
            Ids      = ids?.ToList(),  // 배열 → 리스트 변환
            Offset   = offset,
            Limit    = limit
        };

        // 동적 쿼리 실행 (IUserMapper.xml의 Search 쿼리)
        var users = _userMapper.Search(param);

        // 총 개수 조회 (WHERE 조건 동일, SELECT COUNT(*))
        var total = _userMapper.Count(param);

        // 응답 객체 생성 (익명 타입)
        return Ok(new
        {
            data   = users,                    // 검색 결과 (User 배열)
            total  = total,                    // 총 개수 (페이징 계산용)
            offset = offset ?? 0,              // 현재 오프셋 (null이면 0)
            limit  = limit ?? users.Count      // 현재 리미트 (null이면 전체 개수)
        });
    }

    /*
     * ==================================================================================
     * Create: 새 사용자 생성
     * ==================================================================================
     *
     * HTTP 메서드: POST
     * URL: /api/users
     * 요청 본문: User JSON 객체
     * 응답: 201 Created + Location 헤더 + User 객체
     *
     * 사용 목적:
     * - 회원가입
     * - 관리자가 사용자 추가
     *
     * **[FromBody] 속성:**
     * - HTTP 요청 본문(JSON)을 User 객체로 역직렬화
     * - Content-Type: application/json 헤더 필수
     *
     * **자동 설정 필드:**
     * - CreatedAt: 현재 시간 (DateTime.UtcNow)
     * - IsActive: true (기본값)
     * - Id: Auto-increment (DB에서 자동 생성)
     *
     * **CreatedAtAction 메서드:**
     * - 201 Created 응답
     * - Location 헤더: /api/users/{id} (생성된 리소스 URL)
     * - 응답 본문: 생성된 User 객체 (Id 포함)
     *
     * Location 헤더 예제:
     *     Location: http://localhost:5000/api/users/123
     *
     * 클라이언트는 Location 헤더로 생성된 리소스 조회 가능:
     *     GET http://localhost:5000/api/users/123
     *
     * cURL 예제:
     *     curl -X POST http://localhost:5000/api/users \
     *       -H "Content-Type: application/json" \
     *       -d '{
     *         "userName": "john_doe",
     *         "email": "john@example.com",
     *         "fullName": "John Doe",
     *         "passwordHash": "$2a$10$..."
     *       }'
     *
     * 응답 (201 Created):
     *     {
     *       "id": 123,
     *       "userName": "john_doe",
     *       "email": "john@example.com",
     *       "fullName": "John Doe",
     *       "isActive": true,
     *       "createdAt": "2026-03-01T12:00:00Z"
     *     }
     *
     * **모델 검증:**
     * - [ApiController] 속성으로 자동 검증
     * - User 클래스의 [Required], [EmailAddress] 등 검증
     * - 검증 실패 시 자동으로 400 Bad Request 응답
     *
     * 검증 예제 (User 모델):
     *     public class User
     *     {
     *         [Required]
     *         public string UserName { get; set; }
     *
     *         [Required]
     *         [EmailAddress]
     *         public string Email { get; set; }
     *     }
     *
     * **보안 주의사항:**
     * 1. 비밀번호 평문 저장 금지
     *    - PasswordHash는 BCrypt 등으로 해시된 값
     *    - 클라이언트에서 평문 전송 → 서버에서 해시 후 저장
     *
     * 2. 중복 확인
     *    - UserName, Email 중복 체크 필요
     *    - DB UNIQUE 제약 조건 + 애플리케이션 레벨 검증
     *
     * 3. 입력 검증
     *    - UserName: 영숫자, 밑줄만 허용
     *    - Email: 이메일 형식 검증
     *
     * 실무 개선 사항:
     *     [HttpPost]
     *     public async Task<ActionResult<User>> Create([FromBody] User user)
     *     {
     *         // 중복 체크
     *         if (await _userMapper.ExistsByUserNameAsync(user.UserName))
     *             return Conflict(new { message = "이미 사용 중인 사용자명입니다." });
     *
     *         // 비밀번호 해시
     *         user.PasswordHash = BCrypt.HashPassword(user.Password);
     *
     *         // 기본값 설정
     *         user.CreatedAt = DateTime.UtcNow;
     *         user.IsActive = true;
     *
     *         // DB 저장
     *         await _userMapper.InsertAsync(user);
     *
     *         return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
     *     }
     *
     * NuVatis 매퍼 호출:
     * - _userMapper.Insert(user)
     * - XML: <insert id="Insert">
     * - SQL: INSERT INTO users (...) VALUES (...)
     * - Auto-increment ID는 user.Id에 자동 할당됨
     *
     * ==================================================================================
     */
    [HttpPost]
    public ActionResult<User> Create([FromBody] User user)
    {
        // 기본값 설정
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive  = true;

        // DB 저장 (NuVatis Insert 쿼리 실행)
        _userMapper.Insert(user);

        // 201 Created 응답 + Location 헤더 + User 객체
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /*
     * ==================================================================================
     * Update: 사용자 정보 수정
     * ==================================================================================
     *
     * HTTP 메서드: PUT
     * URL: /api/users/{id}
     * 요청 본문: User JSON 객체
     * 응답: 204 No Content (성공) OR 404 Not Found
     *
     * 사용 목적:
     * - 사용자 프로필 수정
     * - 관리자가 사용자 정보 수정
     *
     * **PUT vs PATCH:**
     *
     * PUT (이 방식):
     * - 전체 리소스 교체
     * - 모든 필드 전송 필요
     * - 멱등성 보장
     *
     * PATCH (부분 수정):
     * - 일부 필드만 수정
     * - 변경된 필드만 전송
     * - JSON Patch 형식
     *
     * **멱등성 (Idempotency):**
     * - 동일한 요청을 여러 번 실행해도 결과 동일
     * - PUT은 멱등성 보장
     * - 예: PUT /api/users/1 {"email": "new@example.com"}
     *   → 몇 번 실행해도 email은 "new@example.com"
     *
     * **204 No Content 응답:**
     * - 성공했지만 응답 본문 없음
     * - 이유: 수정된 데이터는 클라이언트가 이미 알고 있음
     * - 네트워크 대역폭 절약
     *
     * 대안: 200 OK + 수정된 User 객체 반환
     *     _userMapper.Update(user);
     *     return Ok(user);
     *
     * **수정 전 존재 확인:**
     * - GetById로 먼저 조회
     * - 존재하지 않으면 404 Not Found
     * - 이유: 없는 리소스는 수정 불가
     *
     * **ID 덮어쓰기:**
     * - user.Id = id;
     * - 이유: URL의 ID와 본문의 ID가 다를 수 있음
     * - URL의 ID가 우선 (신뢰할 수 있는 출처)
     *
     * **UpdatedAt 자동 설정:**
     * - user.UpdatedAt = DateTime.UtcNow;
     * - 수정 시간 기록
     *
     * cURL 예제:
     *     curl -X PUT http://localhost:5000/api/users/1 \
     *       -H "Content-Type: application/json" \
     *       -d '{
     *         "userName": "john_doe",
     *         "email": "new_email@example.com",
     *         "fullName": "John Doe Updated",
     *         "isActive": true
     *       }'
     *
     * 응답 (204 No Content):
     *     (응답 본문 없음)
     *
     * **보안 주의사항:**
     * 1. 권한 체크
     *    - 일반 사용자는 자신만 수정 가능
     *    - 관리자는 모든 사용자 수정 가능
     *
     * 2. 민감 필드 보호
     *    - PasswordHash는 별도 API로 변경
     *    - IsActive는 관리자만 변경 가능
     *
     * 3. 낙관적 잠금 (Optimistic Locking)
     *    - Version 컬럼 사용
     *    - 동시 수정 방지
     *
     * 실무 개선 사항:
     *     [HttpPut("{id}")]
     *     public async Task<ActionResult> Update(int id, [FromBody] UserUpdateDto dto)
     *     {
     *         var existing = await _userMapper.GetByIdAsync(id);
     *         if (existing == null)
     *             return NotFound();
     *
     *         // 권한 체크
     *         if (!User.IsInRole("Admin") && User.GetUserId() != id)
     *             return Forbid();
     *
     *         // 낙관적 잠금
     *         if (existing.Version != dto.Version)
     *             return Conflict(new { message = "다른 사용자가 이미 수정했습니다." });
     *
     *         // DTO → Entity 매핑
     *         existing.Email = dto.Email;
     *         existing.FullName = dto.FullName;
     *         existing.UpdatedAt = DateTime.UtcNow;
     *         existing.Version++;
     *
     *         await _userMapper.UpdateAsync(existing);
     *
     *         return NoContent();
     *     }
     *
     * **DTO (Data Transfer Object) 사용 권장:**
     * - UserUpdateDto: 수정 가능한 필드만 포함
     * - User 엔티티: 모든 필드 포함 (민감 정보 포함)
     * - 보안: PasswordHash, CreatedAt 등 수정 방지
     *
     * NuVatis 매퍼 호출:
     * - _userMapper.Update(user)
     * - XML: <update id="Update">
     * - SQL: UPDATE users SET ... WHERE id = #{Id}
     *
     * ==================================================================================
     */
    [HttpPut("{id}")]
    public ActionResult Update(int id, [FromBody] User user)
    {
        // 수정 전 존재 확인
        var existing = _userMapper.GetById(id);
        if (existing == null)
            return NotFound(new { message = "사용자를 찾을 수 없습니다." });

        // URL의 ID로 덮어쓰기 (신뢰할 수 있는 출처)
        user.Id        = id;
        user.UpdatedAt = DateTime.UtcNow;

        // DB 업데이트 (NuVatis Update 쿼리 실행)
        _userMapper.Update(user);

        // 204 No Content 응답 (본문 없음)
        return NoContent();
    }

    /*
     * ==================================================================================
     * Delete: 사용자 삭제 (Soft Delete)
     * ==================================================================================
     *
     * HTTP 메서드: DELETE
     * URL: /api/users/{id}
     * 응답: 204 No Content (성공) OR 404 Not Found
     *
     * 사용 목적:
     * - 회원 탈퇴
     * - 관리자가 사용자 삭제
     *
     * **Soft Delete vs Hard Delete:**
     *
     * Soft Delete (이 방식):
     * - DB에서 실제로 삭제하지 않음
     * - deleted_at 컬럼에 삭제 일시 기록
     * - WHERE deleted_at IS NULL로 조회에서 제외
     * - 장점: 복구 가능, 감사 추적, 참조 무결성 유지
     * - 단점: 디스크 공간 사용, 쿼리 복잡도 증가
     *
     * Hard Delete:
     * - DB에서 실제로 삭제 (DELETE FROM users WHERE id = #{Id})
     * - 복구 불가능
     * - 장점: 디스크 절약, 쿼리 단순
     * - 단점: 데이터 손실, 외래키 제약 조건 문제
     *
     * **Soft Delete SQL:**
     *     UPDATE users
     *     SET deleted_at = CURRENT_TIMESTAMP
     *     WHERE id = #{Id} AND deleted_at IS NULL
     *
     * **조회 쿼리 수정 필요:**
     * - 모든 SELECT 쿼리에 WHERE deleted_at IS NULL 추가
     * - IUserMapper.xml의 모든 쿼리 확인
     *
     * **멱등성:**
     * - DELETE는 멱등성 보장
     * - 같은 ID를 여러 번 삭제해도 결과 동일
     * - 이미 삭제된 경우: 404 Not Found (또는 204 No Content)
     *
     * **외래키 제약 조건:**
     *
     * Soft Delete 사용 시:
     * - 외래키 제약 조건 문제 없음
     * - 주문 테이블에서 user_id 참조 유지
     * - 삭제된 사용자의 주문 조회 가능 (이력 보존)
     *
     * Hard Delete 사용 시:
     * - CASCADE: 사용자 삭제 시 주문도 삭제 (위험!)
     * - RESTRICT: 주문이 있으면 사용자 삭제 불가
     * - SET NULL: 주문의 user_id를 NULL로 (비추천)
     *
     * **복구 (Restore):**
     *     <update id="Restore">
     *       UPDATE users
     *       SET deleted_at = NULL
     *       WHERE id = #{Id} AND deleted_at IS NOT NULL
     *     </update>
     *
     * cURL 예제:
     *     curl -X DELETE http://localhost:5000/api/users/1
     *
     * 응답 (204 No Content):
     *     (응답 본문 없음)
     *
     * **보안 주의사항:**
     * 1. 권한 체크
     *    - 일반 사용자는 자신만 삭제 가능
     *    - 관리자는 모든 사용자 삭제 가능
     *    - 단, 자기 자신은 삭제 불가 (관리자 보호)
     *
     * 2. 삭제 전 확인
     *    - 중요한 데이터 (주문, 결제 등) 존재 확인
     *    - 삭제 가능 조건 체크
     *
     * 3. 감사 로그
     *    - 누가, 언제, 왜 삭제했는지 기록
     *    - audit_logs 테이블에 기록
     *
     * 실무 개선 사항:
     *     [HttpDelete("{id}")]
     *     public async Task<ActionResult> Delete(int id)
     *     {
     *         var existing = await _userMapper.GetByIdAsync(id);
     *         if (existing == null)
     *             return NotFound();
     *
     *         // 권한 체크
     *         if (!User.IsInRole("Admin") && User.GetUserId() != id)
     *             return Forbid();
     *
     *         // 자기 자신 삭제 방지 (관리자 보호)
     *         if (User.GetUserId() == id)
     *             return BadRequest(new { message = "자기 자신은 삭제할 수 없습니다." });
     *
     *         // 삭제 가능 조건 체크
     *         var hasOrders = await _orderMapper.ExistsByUserIdAsync(id);
     *         if (hasOrders)
     *             return BadRequest(new { message = "주문 이력이 있는 사용자는 삭제할 수 없습니다." });
     *
     *         // Soft Delete
     *         await _userMapper.SoftDeleteAsync(id);
     *
     *         // 감사 로그 기록
     *         await _auditLogMapper.InsertAsync(new AuditLog
     *         {
     *             Action = "DELETE_USER",
     *             UserId = User.GetUserId(),
     *             TargetId = id,
     *             Timestamp = DateTime.UtcNow
     *         });
     *
     *         return NoContent();
     *     }
     *
     * **GDPR 준수 (개인정보 보호):**
     * - 사용자 요청 시 개인정보 완전 삭제 필요
     * - Soft Delete만으로는 부족
     * - Hard Delete + 익명화 처리
     *
     * 익명화 예제:
     *     <update id="Anonymize">
     *       UPDATE users
     *       SET user_name = 'deleted_' || id,
     *           email = 'deleted_' || id || '@deleted.com',
     *           full_name = '삭제된 사용자',
     *           password_hash = '',
     *           deleted_at = CURRENT_TIMESTAMP
     *       WHERE id = #{Id}
     *     </update>
     *
     * NuVatis 매퍼 호출:
     * - _userMapper.SoftDelete(id)
     * - XML: <update id="SoftDelete">
     * - SQL: UPDATE users SET deleted_at = CURRENT_TIMESTAMP WHERE id = #{Id}
     *
     * ==================================================================================
     */
    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        // 삭제 전 존재 확인
        var existing = _userMapper.GetById(id);
        if (existing == null)
            return NotFound(new { message = "사용자를 찾을 수 없습니다." });

        // Soft Delete (deleted_at 컬럼 업데이트)
        _userMapper.SoftDelete(id);

        // 204 No Content 응답
        return NoContent();
    }
}
