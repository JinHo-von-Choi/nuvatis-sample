namespace NuVatis.Benchmark.Core.Models;

/**
 * 주소 엔티티 (Entity) - 데이터베이스의 addresses 테이블과 1:1 매핑되는 C# 클래스
 *
 * 【 엔티티(Entity)란? 】
 * - 데이터베이스 테이블의 한 행(Row)을 나타내는 C# 객체
 * - ORM(Object-Relational Mapping)의 핵심 개념
 * - SQL: SELECT * FROM addresses → C#: Address 객체
 *
 * 【 테이블 정보 】
 * - 테이블명: addresses
 * - 레코드 수: 150,000개 (사용자당 평균 1.5개 주소)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 * - Foreign Key: user_id → users.id (N:1 관계)
 *
 * 【 실전 활용 】
 * - 배송지 관리: 여러 배송지 등록 (집, 회사, 부모님 댁 등)
 * - 결제 주소: billing (청구지), shipping (배송지) 구분
 * - 기본 주소: is_default = true (결제 시 자동 선택)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class Address
{
    /**
     * 주소 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     * - AUTO_INCREMENT: 자동 증가 (삽입 시 자동 할당)
     */
    public long Id { get; set; }

    /**
     * 사용자 ID (Foreign Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: user_id BIGINT NOT NULL
     * - FK: FOREIGN KEY (user_id) REFERENCES users(id)
     *
     * 【 관계 】
     * - addresses (N) → users (1)
     * - 한 사용자는 여러 주소를 가질 수 있음
     *
     * 【 SQL 예시 】
     * -- 특정 사용자의 모든 주소 조회
     * SELECT * FROM addresses WHERE user_id = 12345;
     *
     * -- 사용자와 주소 JOIN
     * SELECT u.user_name, a.street_address, a.city
     * FROM users u
     * JOIN addresses a ON u.id = a.user_id
     * WHERE u.id = 12345;
     */
    public long UserId { get; set; }

    /**
     * 주소 타입 (배송지 또는 청구지)
     *
     * 【 SQL 매핑 】
     * - 컬럼: address_type VARCHAR(20) NOT NULL
     * - 제약: CHECK (address_type IN ('shipping', 'billing'))
     *
     * 【 가능한 값 】
     * - 'shipping': 배송지 (물건이 배달될 주소)
     * - 'billing': 청구지 (결제 청구서가 발송될 주소)
     *
     * 【 실전 활용 】
     * - 배송지 != 청구지: 선물 구매 시 (배송은 친구 집, 결제는 내 주소)
     * - 법인 구매: 배송은 사무실, 청구는 본사
     *
     * 【 .NET Enum 대안 】
     * public enum AddressType { Shipping, Billing }
     * public AddressType Type { get; set; }
     * → 타입 안전성 향상, 잘못된 값 방지
     */
    public string AddressType { get; set; } = string.Empty;

    /**
     * 도로명 주소 (상세 주소)
     *
     * 【 SQL 매핑 】
     * - 컬럼: street_address VARCHAR(200) NOT NULL
     *
     * 【 예시 】
     * - "서울특별시 강남구 테헤란로 427"
     * - "123 Main Street, Apt 456"
     *
     * 【 주의사항 】
     * - 한글 주소: UTF-8 인코딩 필수
     * - 길이 제한: VARCHAR(200) → 약 66자 (한글 3바이트)
     */
    public string StreetAddress { get; set; } = string.Empty;

    /**
     * 시/구 (City)
     *
     * 【 SQL 매핑 】
     * - 컬럼: city VARCHAR(100) NOT NULL
     *
     * 【 예시 】
     * - "서울", "부산", "New York", "Los Angeles"
     *
     * 【 인덱스 활용 】
     * CREATE INDEX idx_addresses_city ON addresses(city);
     * → 같은 도시의 주소 빠르게 조회 (지역별 통계, 배송 구역 분류)
     */
    public string City { get; set; } = string.Empty;

    /**
     * 주/도 (State/Province) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: state VARCHAR(50) NULL
     *
     * 【 .NET Nullable 개념 】
     * - string?: Nullable Reference Type (C# 8.0+)
     * - NULL 허용: 일부 국가는 state 개념 없음 (한국, 일본)
     *
     * 【 예시 】
     * - 미국: "CA" (California), "NY" (New York)
     * - 한국: null (시/도는 City에 포함)
     *
     * 【 왜 NULL? 】
     * - 국가별 주소 체계 차이
     * - 한국: 시/도/구/동/번지
     * - 미국: Street/City/State/ZIP
     * - 일본: 都道府県/市区町村
     */
    public string? State { get; set; }

    /**
     * 우편번호 (Postal Code / ZIP Code) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: postal_code VARCHAR(20) NULL
     *
     * 【 예시 】
     * - 한국: "06236" (5자리)
     * - 미국: "90210" 또는 "90210-1234" (5자리 또는 9자리)
     * - 영국: "SW1A 1AA" (공백 포함)
     *
     * 【 검증 패턴 (정규식) 】
     * - 한국: ^[0-9]{5}$
     * - 미국: ^[0-9]{5}(-[0-9]{4})?$
     * - 영국: ^[A-Z]{1,2}[0-9]{1,2} [0-9][A-Z]{2}$
     */
    public string? PostalCode { get; set; }

    /**
     * 국가 (Country)
     *
     * 【 SQL 매핑 】
     * - 컬럼: country VARCHAR(50) NOT NULL
     *
     * 【 표준 코드 】
     * [권장] ISO 3166-1 alpha-2 코드 사용:
     * - "KR" (대한민국), "US" (미국), "JP" (일본), "CN" (중국)
     *
     * [비권장] 국가명 직접 저장:
     * - "South Korea", "United States" → 다국어 처리 복잡
     *
     * 【 다국어 처리 】
     * - DB: "KR" 저장
     * - 화면: 사용자 언어에 따라 "대한민국" / "South Korea" 표시
     */
    public string Country { get; set; } = string.Empty;

    /**
     * 기본 주소 여부 (Default Address)
     *
     * 【 SQL 매핑 】
     * - 컬럼: is_default BOOLEAN DEFAULT FALSE
     *
     * 【 실전 활용 】
     * - 결제 시 기본 주소 자동 선택
     *   SELECT * FROM addresses
     *   WHERE user_id = 12345 AND is_default = TRUE
     *   LIMIT 1;
     *
     * - 한 사용자당 1개만 is_default = true
     *   제약: UNIQUE INDEX idx_user_default ON addresses(user_id)
     *         WHERE is_default = TRUE;
     *
     * 【 비즈니스 로직 】
     * - 새 주소를 기본으로 설정 시:
     *   1. 기존 기본 주소를 is_default = false로 변경
     *   2. 새 주소를 is_default = true로 설정
     *   → 트랜잭션으로 묶어야 함 (원자성 보장)
     *
     * BEGIN;
     * UPDATE addresses SET is_default = FALSE WHERE user_id = 12345;
     * UPDATE addresses SET is_default = TRUE WHERE id = 67890;
     * COMMIT;
     */
    public bool IsDefault { get; set; } = false;

    /**
     * 생성 일시 (레코드 최초 삽입 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     *
     * 【 실전 패턴 】
     * - INSERT 시 자동 설정 (DB DEFAULT 활용):
     *   created_at TIMESTAMP DEFAULT NOW()
     * - 또는 애플리케이션에서 설정:
     *   address.CreatedAt = DateTime.UtcNow;
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
     *   UPDATE addresses
     *   SET street_address = ?, updated_at = NOW()
     *   WHERE id = ?;
     */
    public DateTime UpdatedAt { get; set; }

    // ========================================
    // Navigation Properties (탐색 속성)
    // ========================================

    /**
     * 이 주소를 소유한 사용자 (N:1 관계)
     *
     * 【 관계 】
     * - addresses (N) → users (1)
     * - 여러 주소가 한 사용자에게 속함
     *
     * 【 SQL JOIN 예시 】
     * SELECT a.*, u.user_name, u.email
     * FROM addresses a
     * JOIN users u ON a.user_id = u.id
     * WHERE a.id = 12345;
     *
     * 【 .NET 사용 예시 】
     * Address address = await repository.GetByIdAsync(12345);
     * Console.WriteLine($"소유자: {address.User?.UserName}");
     *
     * 【 Lazy Loading 주의 】
     * - User가 null이면: 아직 로드 안 됨 (Lazy Loading)
     * - N+1 문제:
     *   foreach (var addr in addresses) {
     *       Console.WriteLine(addr.User.UserName); // 매번 DB 쿼리 발생!
     *   }
     *
     * [권장] Eager Loading (Include):
     *   var addresses = context.Addresses
     *       .Include(a => a.User)
     *       .ToList();
     *   → 1번의 JOIN 쿼리로 모두 로드
     */
    public User? User { get; set; }
}
