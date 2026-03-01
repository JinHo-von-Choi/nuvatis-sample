namespace NuVatis.Benchmark.Core.Models;

/**
 * 사용자 엔티티 (Entity) - 데이터베이스의 users 테이블과 1:1 매핑되는 C# 클래스
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - ORM(Object-Relational Mapping)의 핵심 개념
 * - SQL: SELECT * FROM users → C#: User 객체
 *
 * 【 테이블 정보 】
 * - 테이블명: users
 * - 레코드 수: 100,000개 (벤치마크용 대용량 데이터)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class User
{
    /**
     * 사용자 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가 (삽입 시 자동 할당)
     *
     * 【 .NET 개념 】
     * - long: 64비트 정수형 (-9경 ~ +9경)
     * - get; set;: 자동 속성 (Auto Property)
     *   컴파일러가 private 필드를 자동 생성
     *   예: private long _id;
     *        public long Id { get => _id; set => _id = value; }
     */
    public long Id { get; set; }

    /**
     * 사용자명 (로그인 ID)
     *
     * 【 SQL 매핑 】
     * - 컬럼: user_name VARCHAR(50) UNIQUE NOT NULL
     * - UNIQUE: 중복 불가 (DB 레벨 제약)
     *
     * 【 .NET 개념 】
     * - string: 참조형 (Reference Type), 기본값 null
     * - = string.Empty: 초기화 (null 대신 빈 문자열 "")
     *   null vs "": null은 값이 없음, ""는 빈 값
     *
     * 【 왜 string.Empty를 사용? 】
     * - NullReferenceException 방지
     * - SQL에서 NOT NULL이므로 null이 들어가면 에러
     */
    public string UserName { get; set; } = string.Empty;

    /**
     * 이메일 주소
     *
     * 【 SQL 매핑 】
     * - 컬럼: email VARCHAR(100) UNIQUE NOT NULL
     * - UNIQUE INDEX: 빠른 검색 (O(log n))
     *
     * 【 실전 활용 】
     * - 회원가입 시 중복 체크: SELECT COUNT(*) FROM users WHERE email = ?
     * - 로그인: SELECT * FROM users WHERE email = ? AND password_hash = ?
     */
    public string Email { get; set; } = string.Empty;

    /**
     * 실명 (한글 또는 영문)
     *
     * 【 SQL 매핑 】
     * - 컬럼: full_name VARCHAR(100) NOT NULL
     *
     * 【 주의사항 】
     * - 한글: UTF-8 인코딩 필수 (PostgreSQL: ENCODING 'UTF8')
     * - "홍길동" → 9바이트 (한글 1자 = 3바이트)
     */
    public string FullName { get; set; } = string.Empty;

    /**
     * 비밀번호 해시 (BCrypt 또는 PBKDF2)
     *
     * 【 보안 개념 】
     * - [금지] 절대 평문 저장 금지: password = "1234"
     * - [권장] 해시 저장: password_hash = "$2a$10$..."
     *
     * 【 BCrypt 예시 】
     * - 입력: "mypassword123"
     * - BCrypt.HashPassword("mypassword123")
     * - 출력: "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy"
     * - 검증: BCrypt.Verify("mypassword123", hash) → true/false
     *
     * 【 왜 해시? 】
     * - 데이터 유출 시에도 원본 비밀번호 노출 방지
     * - 단방향 함수: 해시 → 원본 복원 불가능
     */
    public string PasswordHash { get; set; } = string.Empty;

    /**
     * 생년월일 (선택 항목)
     *
     * 【 SQL 매핑 】
     * - 컬럼: date_of_birth DATE NULL
     *
     * 【 .NET 개념 】
     * - DateTime?: Nullable<DateTime> (값이 없을 수 있음)
     * - 일반 DateTime vs DateTime?
     *   DateTime d = null;        // [에러] 컴파일 에러
     *   DateTime? d = null;       // [정상] OK
     *
     * 【 Nullable 사용법 】
     * - HasValue: 값이 있는지 확인
     *   if (user.DateOfBirth.HasValue) { ... }
     * - Value: 실제 값 가져오기
     *   DateTime birth = user.DateOfBirth.Value;
     * - Null 병합 연산자: ??
     *   DateTime birth = user.DateOfBirth ?? DateTime.Now;
     */
    public DateTime? DateOfBirth { get; set; }

    /**
     * 전화번호 (선택 항목)
     *
     * 【 SQL 매핑 】
     * - 컬럼: phone_number VARCHAR(20) NULL
     *
     * 【 .NET 개념 】
     * - string?: Nullable Reference Type (C# 8.0+)
     * - 컴파일러가 null 체크 경고
     *   string? phone = user.PhoneNumber;
     *   int len = phone.Length; // [경고] null일 수 있음
     *   int len = phone?.Length ?? 0; // [안전] Null 안전
     */
    public string? PhoneNumber { get; set; }

    /**
     * 활성 상태 (true: 활성, false: 비활성/탈퇴)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_active BOOLEAN DEFAULT TRUE
     *
     * 【 실전 활용 】
     * - 소프트 삭제 (Soft Delete): 실제로 DELETE 하지 않고 플래그만 변경
     *   DELETE FROM users WHERE id = 1;           // [비권장] 물리 삭제
     *   UPDATE users SET is_active = FALSE WHERE id = 1; // [권장] 논리 삭제
     *
     * - 로그인 체크:
     *   WHERE email = ? AND password_hash = ? AND is_active = TRUE
     *
     * 【 왜 소프트 삭제? 】
     * - 복구 가능 (탈퇴 후 재가입)
     * - 통계 유지 (탈퇴 회원도 주문 내역 조회)
     * - 법적 요구사항 (개인정보 보관 기간)
     */
    public bool IsActive { get; set; } = true;

    /**
     * 생성 일시 (레코드 최초 삽입 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 .NET 개념 】
     * - DateTime: 날짜 + 시간 (년월일시분초)
     * - DateTime.Now: 현재 시각 (서버 기준)
     * - DateTime.UtcNow: UTC 시각 (권장, 타임존 독립적)
     *
     * 【 실전 패턴 】
     * - INSERT 시 자동 설정:
     *   user.CreatedAt = DateTime.UtcNow;
     * - 또는 DB DEFAULT 활용 (권장):
     *   created_at TIMESTAMP DEFAULT NOW()
     */
    public DateTime CreatedAt { get; set; }

    /**
     * 수정 일시 (레코드 최종 업데이트 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 패턴 】
     * - UPDATE 시마다 갱신:
     *   UPDATE users SET full_name = ?, updated_at = NOW() WHERE id = ?
     *
     * 【 감사 추적 (Audit Trail) 】
     * - 언제 마지막으로 수정되었는지 추적
     * - 비정상 활동 탐지 (예: 새벽 2시 정보 변경)
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================
    // 【 개념 】
    // - ORM에서 테이블 간 관계를 C# 객체 관계로 표현
    // - SQL JOIN을 C# 속성 접근으로 대체
    //   SQL: SELECT * FROM users u JOIN addresses a ON u.id = a.user_id
    //   C#:  user.Addresses.First().Street
    //
    // 【 주의사항 】
    // - Lazy Loading: 첫 접근 시 DB 쿼리 발생 (N+1 문제 주의)
    // - Eager Loading: 미리 JOIN으로 가져오기 (Include())
    //   context.Users.Include(u => u.Addresses).ToList();

    /**
     * 사용자의 주소 목록 (1:N 관계)
     *
     * 【 SQL 관계 】
     * - users (1) <--> (N) addresses
     * - FK: addresses.user_id → users.id
     *
     * 【 SQL 예시 】
     * SELECT a.*
     * FROM addresses a
     * WHERE a.user_id = 12345;
     *
     * 【 .NET 개념 】
     * - ICollection<T>: List<T>보다 추상화된 컬렉션
     * - ?: Nullable (주소가 없을 수도 있음)
     *
     * 【 사용 예시 】
     * foreach (var addr in user.Addresses ?? Enumerable.Empty<Address>())
     * {
     *     Console.WriteLine(addr.Street);
     * }
     */
    public ICollection<Address>? Addresses { get; set; }

    /**
     * 사용자의 주문 목록 (1:N 관계)
     *
     * 【 SQL 관계 】
     * - users (1) <--> (N) orders
     * - FK: orders.user_id → users.id
     *
     * 【 실전 활용 】
     * - 주문 내역 조회:
     *   SELECT o.* FROM orders o WHERE o.user_id = 12345 ORDER BY o.created_at DESC;
     *
     * - 총 주문 금액:
     *   SELECT SUM(o.total_amount) FROM orders o WHERE o.user_id = 12345;
     */
    public ICollection<Order>? Orders { get; set; }

    /**
     * 사용자가 작성한 리뷰 목록 (1:N 관계)
     *
     * 【 SQL 관계 】
     * - users (1) <--> (N) reviews
     * - FK: reviews.user_id → users.id
     */
    public ICollection<Review>? Reviews { get; set; }

    /**
     * 사용자가 보유한 쿠폰 목록 (N:M 관계)
     *
     * 【 SQL 관계 】
     * - users (N) <--> (M) coupons
     * - 중간 테이블: user_coupons (user_id, coupon_id)
     *
     * 【 N:M 관계 JOIN 예시 】
     * SELECT c.*
     * FROM coupons c
     * JOIN user_coupons uc ON c.id = uc.coupon_id
     * WHERE uc.user_id = 12345;
     */
    public ICollection<UserCoupon>? UserCoupons { get; set; }

    /**
     * 사용자의 위시리스트 (찜 목록) (N:M 관계)
     *
     * 【 SQL 관계 】
     * - users (N) <--> (M) products
     * - 중간 테이블: wishlists (user_id, product_id)
     */
    public ICollection<Wishlist>? Wishlists { get; set; }
}
