using Npgsql;
using System.Diagnostics;

namespace NuVatis.Benchmark.Runner.Helpers;

/**
 * 데이터베이스 스키마 및 테스트 데이터 초기화 헬퍼
 *
 * 작성자: 최진호
 * 작성일: 2026-03-04
 */
public static class DatabaseInitializer
{
    /**
     * 스키마 및 테스트 데이터 초기화 (멱등성 보장)
     *
     * @param connectionString PostgreSQL 연결 문자열
     * @param forceReset true면 기존 데이터 삭제 후 재생성
     * @return 초기화 성공 여부
     */
    public static async Task<bool> InitializeAsync(string connectionString, bool forceReset = false)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("데이터베이스 초기화 시작");
        Console.WriteLine("========================================");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. 스키마 존재 여부 확인
            var schemaExists = await CheckSchemaExistsAsync(connection);

            if (schemaExists && !forceReset)
            {
                // 2. 테이블에 데이터가 있는지 확인
                var hasData = await CheckDataExistsAsync(connection);

                if (hasData)
                {
                    Console.WriteLine("✓ 스키마 및 데이터가 이미 존재합니다. 초기화를 건너뜁니다.");
                    stopwatch.Stop();
                    Console.WriteLine($"소요 시간: {stopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine("========================================\n");
                    return true;
                }
            }

            // 3. 스키마 초기화
            if (!schemaExists || forceReset)
            {
                Console.WriteLine("스키마 생성 중...");
                await ExecuteSqlFileAsync(connection, "Scripts/init-schema.sql");
                Console.WriteLine("✓ 스키마 생성 완료");
            }

            // 4. 테스트 데이터 생성
            Console.WriteLine("테스트 데이터 생성 중 (10만 로우+)...");
            await ExecuteSqlFileAsync(connection, "Scripts/generate-testdata.sql");
            Console.WriteLine("✓ 테스트 데이터 생성 완료");

            stopwatch.Stop();
            Console.WriteLine($"총 소요 시간: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine("========================================\n");

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ 데이터베이스 초기화 실패: {ex.Message}");
            Console.Error.WriteLine($"상세: {ex}");
            return false;
        }
    }

    /**
     * 스키마 존재 여부 확인
     */
    private static async Task<bool> CheckSchemaExistsAsync(NpgsqlConnection connection)
    {
        const string query = "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'nuvatest')";

        await using var command = new NpgsqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync();

        return result is bool exists && exists;
    }

    /**
     * 테이블에 데이터가 있는지 확인
     */
    private static async Task<bool> CheckDataExistsAsync(NpgsqlConnection connection)
    {
        const string query = @"
            SELECT
                (SELECT COUNT(*) FROM nuvatest.users) +
                (SELECT COUNT(*) FROM nuvatest.addresses) +
                (SELECT COUNT(*) FROM nuvatest.categories) +
                (SELECT COUNT(*) FROM nuvatest.products) +
                (SELECT COUNT(*) FROM nuvatest.orders) +
                (SELECT COUNT(*) FROM nuvatest.order_items) +
                (SELECT COUNT(*) FROM nuvatest.reviews) AS total_rows";

        try
        {
            await using var command = new NpgsqlCommand(query, connection);
            var result = await command.ExecuteScalarAsync();

            if (result is long totalRows)
            {
                Console.WriteLine($"현재 데이터: {totalRows:N0} rows");
                return totalRows >= 100000; // 10만 로우 이상이면 데이터가 있다고 판단
            }

            return false;
        }
        catch (NpgsqlException)
        {
            // 테이블이 없으면 예외 발생 (데이터 없음)
            return false;
        }
    }

    /**
     * SQL 파일 실행 (전체를 한 번에 실행하여 DO 블록 등 복잡한 구문 지원)
     */
    private static async Task ExecuteSqlFileAsync(NpgsqlConnection connection, string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"SQL 파일을 찾을 수 없습니다: {filePath}");
        }

        var sql = await File.ReadAllTextAsync(filePath);

        try
        {
            // SQL 파일 전체를 한 번에 실행 (DO 블록, 트리거, 함수 등 지원)
            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 600; // 10분 타임아웃 (대량 데이터 생성)
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ SQL 파일 실행 실패: {ex.Message}");
            Console.Error.WriteLine($"파일: {filePath}");
            throw;
        }
    }

    /**
     * 강제 재초기화 (기존 데이터 삭제)
     */
    public static async Task<bool> ResetAsync(string connectionString)
    {
        Console.WriteLine("기존 데이터를 삭제하고 재초기화합니다...");
        return await InitializeAsync(connectionString, forceReset: true);
    }
}
