namespace NuVatis.Benchmark.Core.Models;

/**
 * 감사 로그 엔티티 (Entity) - 모든 데이터 변경 이력 추적
 *
 * 【 테이블 정보 】
 * - 테이블명: audit_logs
 * - 레코드 수: 50,000,000개 (5천만 건, 모든 변경 이력)
 * - Primary Key: id (BIGINT, AUTO_INCREMENT)
 *
 * 【 감사 로그(Audit Log)란? 】
 * - 누가(Who), 언제(When), 무엇을(What), 어떻게(How) 변경했는지 기록
 * - 컴플라이언스(Compliance): 법적 요구사항 충족 (GDPR, PCI-DSS 등)
 * - 보안 감사: 비정상 활동 탐지 (해킹, 내부자 위협)
 * - 데이터 복구: 실수로 삭제한 데이터 복원
 * - 분쟁 해결: 언제 어떻게 변경되었는지 증명
 *
 * 【 실전 활용 】
 * - 관리자가 사용자 정보 수정 → 로그 기록
 * - 상품 가격 변경 → 변경 전/후 가격 저장
 * - 주문 취소 → 취소 사유 및 처리자 기록
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */
public class AuditLog
{
    /**
     * 감사 로그 고유 식별자 (Primary Key)
     *
     * 【 SQL 매핑 】
     * - 컬럼: id BIGINT PRIMARY KEY
     */
    public long Id { get; set; }

    /**
     * 테이블명 (Table Name)
     *
     * 【 SQL 매핑 】
     * - 컬럼: table_name VARCHAR(50) NOT NULL
     * - INDEX: idx_audit_logs_table (테이블별 로그 조회)
     *
     * 【 추적 대상 테이블 】
     * - "users": 사용자 정보 변경
     * - "products": 상품 정보 변경
     * - "orders": 주문 정보 변경
     * - "payments": 결제 정보 변경
     *
     * 【 SQL 예시 】
     * -- users 테이블 변경 이력 조회
     * SELECT * FROM audit_logs
     * WHERE table_name = 'users'
     * ORDER BY created_at DESC
     * LIMIT 100;
     *
     * -- 테이블별 변경 빈도
     * SELECT table_name,
     *        COUNT(*) AS change_count
     * FROM audit_logs
     * WHERE created_at >= NOW() - INTERVAL '7 days'
     * GROUP BY table_name
     * ORDER BY change_count DESC;
     *
     * 결과:
     * | table_name | change_count |
     * |------------|--------------|
     * | orders     | 50,000       |
     * | products   | 5,000        |
     * | users      | 2,000        |
     */
    public string TableName { get; set; } = string.Empty;

    /**
     * 레코드 ID (Record ID)
     *
     * 【 SQL 매핑 】
     * - 컬럼: record_id BIGINT NOT NULL
     * - INDEX: idx_audit_logs_table_record (특정 레코드 이력 조회)
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_audit_logs_table_record
     * ON audit_logs(table_name, record_id, created_at DESC);
     * → 특정 테이블의 특정 레코드 변경 이력 빠르게 조회
     *
     * 【 실전 활용 】
     * - table_name = "users", record_id = 12345 → 사용자 ID 12345의 변경 이력
     * - table_name = "products", record_id = 123 → 상품 ID 123의 변경 이력
     *
     * 【 SQL 예시 】
     * -- 특정 사용자의 변경 이력 조회
     * SELECT * FROM audit_logs
     * WHERE table_name = 'users'
     *   AND record_id = 12345
     * ORDER BY created_at DESC;
     *
     * -- 특정 상품의 가격 변경 이력
     * SELECT id, action, old_values, new_values, created_at
     * FROM audit_logs
     * WHERE table_name = 'products'
     *   AND record_id = 123
     *   AND (old_values->>'price' IS DISTINCT FROM new_values->>'price')
     * ORDER BY created_at DESC;
     *
     * → JSONB 필드에서 price 변경 사항만 필터링
     */
    public long RecordId { get; set; }

    /**
     * 작업 타입 (Action)
     *
     * 【 SQL 매핑 】
     * - 컬럼: action VARCHAR(10) NOT NULL
     * - 제약: CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
     *
     * 【 작업 타입 설명 】
     * - 'INSERT': 신규 레코드 생성
     *   old_values = null, new_values = 생성된 데이터
     *
     * - 'UPDATE': 기존 레코드 수정
     *   old_values = 변경 전 데이터, new_values = 변경 후 데이터
     *
     * - 'DELETE': 레코드 삭제 (소프트 삭제 포함)
     *   old_values = 삭제된 데이터, new_values = null
     *
     * 【 SQL 예시 】
     * -- 작업 타입별 통계
     * SELECT action,
     *        COUNT(*) AS count
     * FROM audit_logs
     * WHERE created_at >= NOW() - INTERVAL '7 days'
     * GROUP BY action
     * ORDER BY count DESC;
     *
     * 결과:
     * | action | count  |
     * |--------|--------|
     * | UPDATE | 30,000 | ← 대부분 수정
     * | INSERT | 15,000 |
     * | DELETE | 500    |
     */
    public string Action { get; set; } = string.Empty;

    /**
     * 변경 전 값 (Old Values) - JSON 형식
     *
     * 【 SQL 매핑 】
     * - 컬럼: old_values JSONB NULL (PostgreSQL)
     * - 컬럼: old_values JSON NULL (MySQL)
     *
     * 【 .NET string 타입 】
     * - string?: JSON 문자열 저장
     * - null: INSERT 작업 (변경 전 데이터 없음)
     * - 값 있음: UPDATE, DELETE 작업
     *
     * 【 JSON 예시 】
     * INSERT: old_values = null
     *
     * UPDATE: old_values = {
     *   "user_name": "old_username",
     *   "email": "old@example.com",
     *   "full_name": "Old Name"
     * }
     *
     * DELETE: old_values = {
     *   "id": 12345,
     *   "user_name": "deleted_user",
     *   ...
     * }
     *
     * 【 C# JSON 직렬화 】
     * using System.Text.Json;
     *
     * var oldValues = new {
     *     user_name = "old_username",
     *     email = "old@example.com"
     * };
     *
     * auditLog.OldValues = JsonSerializer.Serialize(oldValues);
     *
     * 【 PostgreSQL JSONB 쿼리 】
     * -- 특정 필드 변경 조회
     * SELECT * FROM audit_logs
     * WHERE table_name = 'users'
     *   AND old_values->>'email' LIKE '%@example.com';
     *
     * -- 변경 전 가격 조회
     * SELECT (old_values->>'price')::DECIMAL AS old_price,
     *        (new_values->>'price')::DECIMAL AS new_price
     * FROM audit_logs
     * WHERE table_name = 'products'
     *   AND record_id = 123
     *   AND action = 'UPDATE';
     */
    public string? OldValues { get; set; }

    /**
     * 변경 후 값 (New Values) - JSON 형식
     *
     * 【 SQL 매핑 】
     * - 컬럼: new_values JSONB NULL (PostgreSQL)
     * - 컬럼: new_values JSON NULL (MySQL)
     *
     * 【 .NET string 타입 】
     * - string?: JSON 문자열 저장
     * - null: DELETE 작업 (변경 후 데이터 없음)
     * - 값 있음: INSERT, UPDATE 작업
     *
     * 【 JSON 예시 】
     * INSERT: new_values = {
     *   "id": 12345,
     *   "user_name": "new_user",
     *   "email": "new@example.com",
     *   ...
     * }
     *
     * UPDATE: new_values = {
     *   "user_name": "new_username",
     *   "email": "new@example.com",
     *   "full_name": "New Name"
     * }
     *
     * DELETE: new_values = null
     *
     * 【 변경 사항 비교 】
     * -- old_values와 new_values 비교 (변경된 필드만 추출)
     * SELECT
     *   record_id,
     *   jsonb_object_keys(new_values) AS field,
     *   old_values->>jsonb_object_keys(new_values) AS old_value,
     *   new_values->>jsonb_object_keys(new_values) AS new_value
     * FROM audit_logs
     * WHERE id = 12345
     *   AND action = 'UPDATE';
     *
     * 결과:
     * | record_id | field     | old_value       | new_value       |
     * |-----------|-----------|-----------------|-----------------|
     * | 123       | email     | old@example.com | new@example.com |
     * | 123       | full_name | Old Name        | New Name        |
     */
    public string? NewValues { get; set; }

    /**
     * 변경자 ID (Changed By) - 선택 항목
     *
     * 【 SQL 매핑 】
     * - 컬럼: changed_by BIGINT NULL
     * - INDEX: idx_audit_logs_changed_by (사용자별 변경 이력 조회)
     *
     * 【 .NET Nullable 개념 】
     * - long?: Nullable<long>
     * - null: 시스템 자동 변경 (배치 작업, 트리거 등)
     * - 값 있음: 사용자 ID (관리자, 일반 사용자)
     *
     * 【 실전 활용 】
     * -- 특정 관리자의 변경 이력 조회
     * SELECT * FROM audit_logs
     * WHERE changed_by = 12345
     * ORDER BY created_at DESC
     * LIMIT 100;
     *
     * -- 관리자별 변경 횟수 (활동 내역)
     * SELECT u.user_name,
     *        COUNT(al.id) AS change_count
     * FROM audit_logs al
     * JOIN users u ON al.changed_by = u.id
     * WHERE al.created_at >= NOW() - INTERVAL '30 days'
     * GROUP BY u.id, u.user_name
     * ORDER BY change_count DESC;
     *
     * 【 보안 감사 】
     * -- 비정상 시간대 변경 (새벽 2~5시)
     * SELECT * FROM audit_logs
     * WHERE changed_by IS NOT NULL
     *   AND EXTRACT(HOUR FROM created_at) BETWEEN 2 AND 5
     * ORDER BY created_at DESC;
     *
     * → 새벽 시간대 관리자 활동 (보안 위협 가능성)
     */
    public long? ChangedBy { get; set; }

    /**
     * 생성 일시 (변경 발생 시각)
     *
     * 【 SQL 매핑 】
     * - 컬럼: created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
     * - INDEX: idx_audit_logs_created_at (시계열 조회)
     *
     * 【 복합 인덱스 】
     * CREATE INDEX idx_audit_logs_table_record_created
     * ON audit_logs(table_name, record_id, created_at DESC);
     * → 특정 레코드의 변경 이력을 시간순으로 빠르게 조회
     *
     * 【 실전 쿼리 】
     * -- 최근 24시간 변경 이력
     * SELECT * FROM audit_logs
     * WHERE created_at >= NOW() - INTERVAL '24 hours'
     * ORDER BY created_at DESC
     * LIMIT 1000;
     *
     * -- 시간대별 변경 빈도 (활동 패턴 분석)
     * SELECT EXTRACT(HOUR FROM created_at) AS hour,
     *        COUNT(*) AS change_count
     * FROM audit_logs
     * WHERE created_at >= NOW() - INTERVAL '7 days'
     * GROUP BY hour
     * ORDER BY hour;
     *
     * 【 데이터 복구 】
     * -- 실수로 삭제한 데이터 복구
     * SELECT old_values
     * FROM audit_logs
     * WHERE table_name = 'users'
     *   AND record_id = 12345
     *   AND action = 'DELETE'
     * ORDER BY created_at DESC
     * LIMIT 1;
     *
     * → old_values에서 삭제된 데이터 추출하여 INSERT로 복구
     *
     * 【 변경 추적 (Who, When, What) 】
     * SELECT
     *   u.user_name AS who,
     *   al.created_at AS when,
     *   al.table_name || '.' || al.record_id AS what,
     *   al.action AS how,
     *   al.old_values->>'email' AS old_email,
     *   al.new_values->>'email' AS new_email
     * FROM audit_logs al
     * LEFT JOIN users u ON al.changed_by = u.id
     * WHERE al.table_name = 'users'
     *   AND al.record_id = 12345
     * ORDER BY al.created_at DESC;
     *
     * 결과:
     * | who        | when                | what       | how    | old_email       | new_email       |
     * |------------|---------------------|------------|--------|-----------------|-----------------|
     * | admin_user | 2026-03-01 14:30:00 | users.12345| UPDATE | old@example.com | new@example.com |
     */
    public DateTime CreatedAt { get; set; }
}
