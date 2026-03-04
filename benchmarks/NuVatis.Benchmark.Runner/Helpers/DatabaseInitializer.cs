using Npgsql;
using NuVatis.Statement;
using System.Diagnostics;
using System.Xml.Linq;

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

    /**
     * XML 매퍼 파일을 파싱하여 MappedStatement를 statements 딕셔너리에 등록한다.
     *
     * SqlSessionFactoryBuilder.BuildConfiguration()이 _xmlConfigPath를 무시하는 버그를
     * 우회하기 위해 런타임에 직접 XML을 파싱하여 Statements를 채운다.
     *
     * 동적 SQL 태그(<where>, <if>, <foreach>)는 element.Value.Trim()으로 텍스트만 추출되어
     * 해당 구문의 SQL이 불완전해질 수 있다. 정적 SQL 구문은 정상 동작한다.
     *
     * @param statements NuVatisConfiguration.Statements 딕셔너리 (mutable)
     * @param xmlPaths   파싱할 XML 매퍼 파일 경로 목록
     */
    public static void LoadXmlMappers(Dictionary<string, MappedStatement> statements, params string[] xmlPaths)
    {
        foreach (var path in xmlPaths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"XML 매퍼 파일을 찾을 수 없습니다: {path}");

            var doc       = XDocument.Load(path);
            var mapper    = doc.Root ?? throw new InvalidOperationException($"XML 루트 요소가 없습니다: {path}");
            var ns        = mapper.Attribute("namespace")?.Value
                            ?? throw new InvalidOperationException($"mapper 요소에 namespace 속성이 없습니다: {path}");

            var elementTypeMap = new Dictionary<string, NuVatis.Statement.StatementType>(StringComparer.OrdinalIgnoreCase) {
                { "select", NuVatis.Statement.StatementType.Select },
                { "insert", NuVatis.Statement.StatementType.Insert },
                { "update", NuVatis.Statement.StatementType.Update },
                { "delete", NuVatis.Statement.StatementType.Delete }
            };

            foreach (var element in mapper.Elements())
            {
                if (!elementTypeMap.TryGetValue(element.Name.LocalName, out var statementType))
                    continue;

                var id = element.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id))
                    continue;

                var resultMapId = element.Attribute("resultMap")?.Value;
                var sqlSource   = element.Value.Trim();

                var statement = new MappedStatement {
                    Id          = id,
                    Namespace   = ns,
                    Type        = statementType,
                    SqlSource   = sqlSource,
                    ResultMapId = resultMapId
                };

                statements[statement.FullId] = statement;
            }

            Console.WriteLine($"✓ XML 매퍼 로드: {Path.GetFileName(path)} ({statements.Count}개 구문 등록)");
        }
    }

    /**
     * BenchmarkDotNet isolated process 환경에서 Mappers/Xml 디렉토리를 탐색
     *
     * BenchmarkDotNet은 각 벤치마크를 {output}/{guid}/bin/Release/net8.0/ 에서 실행한다.
     * AppContext.BaseDirectory가 isolated 디렉토리를 가리키므로
     * XML 파일이 있는 실제 output 디렉토리까지 부모 디렉토리를 재귀 탐색한다.
     *
     * @param mapperXmlSubPath Mappers/Xml 하위의 상대 경로 (예: "IUserMapper.xml")
     * @return XML 파일의 절대 경로
     * @throws DirectoryNotFoundException Mappers/Xml 디렉토리를 찾지 못한 경우
     */
    public static string FindXmlFile(string mapperXmlSubPath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var xmlDir = Path.Combine(dir.FullName, "Mappers", "Xml");
            if (Directory.Exists(xmlDir))
                return Path.Combine(xmlDir, mapperXmlSubPath);
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Mappers/Xml 디렉토리를 찾을 수 없습니다. 탐색 시작: {AppContext.BaseDirectory}");
    }
}
