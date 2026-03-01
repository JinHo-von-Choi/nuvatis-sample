/*
 * ==================================================================================
 * User 엔티티 - 데이터베이스 테이블 매핑
 * ==================================================================================
 *
 * 이 클래스는 PostgreSQL users 테이블과 매핑되는 엔티티입니다.
 * NuVatis는 이 클래스의 속성을 테이블 컬럼에 자동으로 매핑합니다.
 *
 * ==================================================================================
 * 테이블 스키마 (database/schema.sql)
 * ==================================================================================
 *
 * CREATE TABLE users (
 *     id          SERIAL PRIMARY KEY,
 *     user_name   VARCHAR(50) NOT NULL UNIQUE,
 *     email       VARCHAR(255) NOT NULL UNIQUE,
 *     full_name   VARCHAR(100),
 *     created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
 *     updated_at  TIMESTAMP,
 *     is_active   BOOLEAN NOT NULL DEFAULT true,
 *     deleted_at  TIMESTAMP
 * );
 *
 * CREATE INDEX idx_users_email ON users(email);
 * CREATE INDEX idx_users_is_active ON users(is_active);
 * CREATE INDEX idx_users_deleted_at ON users(deleted_at);
 *
 * ==================================================================================
 * 매핑 규칙
 * ==================================================================================
 *
 * **컬럼명 변환:**
 * - C# 속성: PascalCase (Id, UserName, CreatedAt)
 * - DB 컬럼: snake_case (id, user_name, created_at)
 * - NuVatis가 자동으로 변환하지 않으므로 XML ResultMap에서 명시적 매핑 필요
 *
 * **NULL 허용:**
 * - C#: string? (nullable reference type)
 * - DB: NULL 허용 컬럼
 * - 예: FullName, UpdatedAt
 *
 * **기본값:**
 * - C#: = string.Empty, = true
 * - DB: DEFAULT 제약 조건
 * - 예: IsActive = true, CreatedAt = CURRENT_TIMESTAMP
 *
 * ==================================================================================
 * 속성 설명
 * ==================================================================================
 *
 * Id (int):
 *  - 테이블: id SERIAL PRIMARY KEY
 *  - 자동 증가 (Auto-increment)
 *  - INSERT 후 NuVatis가 자동으로 할당
 *  - 변경 불가 (Read-only after insert)
 *
 * UserName (string):
 *  - 테이블: user_name VARCHAR(50) NOT NULL UNIQUE
 *  - 로그인 ID
 *  - 중복 불가 (UNIQUE 제약 조건)
 *  - 검증 필요: 영숫자, 밑줄, 하이픈만 허용
 *  - 예: "john_doe", "user-123"
 *
 * Email (string):
 *  - 테이블: email VARCHAR(255) NOT NULL UNIQUE
 *  - 이메일 주소
 *  - 중복 불가 (UNIQUE 제약 조건)
 *  - 검증 필요: 이메일 형식 (user@example.com)
 *  - 인덱스 존재 (빠른 검색)
 *
 * FullName (string?):
 *  - 테이블: full_name VARCHAR(100)
 *  - 실명 (선택 사항)
 *  - NULL 허용
 *  - 예: "홍길동", "John Doe"
 *
 * CreatedAt (DateTime):
 *  - 테이블: created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
 *  - 생성 일시
 *  - INSERT 시 자동 설정 (DB 또는 애플리케이션)
 *  - 변경 불가 (Read-only after insert)
 *
 * UpdatedAt (DateTime?):
 *  - 테이블: updated_at TIMESTAMP
 *  - 수정 일시
 *  - UPDATE 시마다 갱신
 *  - NULL 허용 (생성 직후에는 NULL)
 *
 * IsActive (bool):
 *  - 테이블: is_active BOOLEAN NOT NULL DEFAULT true
 *  - 활성 여부
 *  - true: 정상 사용자, false: 비활성화
 *  - 기본값: true
 *  - Soft Delete와 별개 (deleted_at과 다름)
 *
 * ==================================================================================
 * 누락된 속성
 * ==================================================================================
 *
 * DB에는 있지만 C# 모델에 없는 컬럼:
 *
 * deleted_at (TIMESTAMP):
 *  - Soft Delete 구현용
 *  - NULL: 정상, NOT NULL: 삭제됨
 *  - C# 모델에 추가하려면:
 *      public DateTime? DeletedAt { get; set; }
 *
 * password_hash (VARCHAR):
 *  - 비밀번호 해시
 *  - 보안상 이유로 모델에서 제외 (DTO로 분리 권장)
 *
 * ==================================================================================
 * 사용 예제
 * ==================================================================================
 *
 * 생성:
 *     var user = new User
 *     {
 *         UserName = "john_doe",
 *         Email = "john@example.com",
 *         FullName = "John Doe",
 *         CreatedAt = DateTime.UtcNow,
 *         IsActive = true
 *     };
 *     _userMapper.Insert(user);
 *     Console.WriteLine($"생성된 ID: {user.Id}");  // Auto-increment ID
 *
 * 조회:
 *     var user = _userMapper.GetById(123);
 *     if (user != null)
 *         Console.WriteLine($"{user.FullName} ({user.Email})");
 *
 * 수정:
 *     user.Email = "new_email@example.com";
 *     user.UpdatedAt = DateTime.UtcNow;
 *     _userMapper.Update(user);
 *
 * 삭제 (Soft):
 *     _userMapper.SoftDelete(user.Id);  // deleted_at = CURRENT_TIMESTAMP
 *
 * ==================================================================================
 * 검증 규칙 (Data Annotations - 선택)
 * ==================================================================================
 *
 * API에서 사용할 경우 검증 속성 추가 권장:
 *
 *     [Required(ErrorMessage = "사용자명은 필수입니다.")]
 *     [StringLength(50, MinimumLength = 3)]
 *     [RegularExpression(@"^[a-zA-Z0-9_-]+$")]
 *     public string UserName { get; set; } = string.Empty;
 *
 *     [Required]
 *     [EmailAddress]
 *     public string Email { get; set; } = string.Empty;
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
 * 사용자 엔티티
 * PostgreSQL users 테이블과 매핑
 */
public class User
{
    /** 사용자 ID (PK, Auto-increment) */
    public int Id { get; set; }

    /** 사용자명 (로그인 ID, UNIQUE) */
    public string UserName { get; set; } = string.Empty;

    /** 이메일 주소 (UNIQUE) */
    public string Email { get; set; } = string.Empty;

    /** 실명 (선택 사항, NULL 허용) */
    public string? FullName { get; set; }

    /** 생성 일시 (자동 설정, 변경 불가) */
    public DateTime CreatedAt { get; set; }

    /** 수정 일시 (NULL 허용, UPDATE 시마다 갱신) */
    public DateTime? UpdatedAt { get; set; }

    /** 활성 여부 (기본값: true) */
    public bool IsActive { get; set; } = true;
}
