using Npgsql;
using NuVatis.Statement;
using System.Diagnostics;
using System.Text;
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
     * <foreach> 템플릿 정보 (LoadXmlMappers 파싱 결과)
     *
     * ForeachExpandInterceptor가 BeforeExecute에서 이 딕셔너리를 조회하여
     * __FOREACH_PLACEHOLDER__를 실제 SQL로 확장한다.
     *
     * 키: MappedStatement.FullId (namespace.id 형식)
     */
    public static readonly Dictionary<string, ForeachTemplateInfo> ForeachTemplates = new();

    /**
     * <foreach> XML 어트리뷰트 및 바디 정보를 담는 레코드
     */
    public sealed class ForeachTemplateInfo
    {
        public required string CollectionName { get; init; }
        public required string ItemName       { get; init; }
        public required string Separator      { get; init; }
        public required string BodyTemplate   { get; init; }
    }

    /**
     * <where><if> 템플릿 정보 (LoadXmlMappers 파싱 결과)
     *
     * WhereIfExpandInterceptor가 BeforeExecute에서 이 딕셔너리를 조회하여
     * __WHERE_PLACEHOLDER__를 실제 WHERE 절로 확장한다.
     *
     * 키: MappedStatement.FullId (namespace.id 형식)
     */
    public static readonly Dictionary<string, WhereTemplateInfo> WhereTemplates = new();

    /**
     * <where><if> 조건 목록을 담는 레코드
     */
    public sealed class WhereTemplateInfo
    {
        public required IReadOnlyList<WhereCondition> Conditions { get; init; }
    }

    /**
     * 단일 <if> 조건을 담는 레코드
     *
     * TestExpression: <if test="..."> 어트리뷰트 값 (예: "UserName != null")
     * SqlFragment:    <if> 바디 텍스트 (예: "AND user_name LIKE '%' || #{UserName} || '%'")
     */
    public sealed class WhereCondition
    {
        public required string TestExpression { get; init; }
        public required string SqlFragment    { get; init; }
    }

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

            // <sql id="..."> 프래그먼트를 미리 수집하여 <include refid="..."/> 확장에 사용한다.
            var sqlFragments = mapper.Elements()
                .Where(e => e.Name.LocalName == "sql")
                .Where(e => !string.IsNullOrEmpty(e.Attribute("id")?.Value))
                .ToDictionary(e => e.Attribute("id")!.Value, e => e.Value.Trim());

            foreach (var element in mapper.Elements())
            {
                if (!elementTypeMap.TryGetValue(element.Name.LocalName, out var statementType))
                    continue;

                var id = element.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id))
                    continue;

                var resultMapId = element.Attribute("resultMap")?.Value;
                var fullId      = ns + "." + id;

                // <foreach>, <where> 자식 요소를 감지하여 플레이스홀더 방식으로 처리한다.
                // element.Value.Trim()은 모든 자식 텍스트를 연결하므로 동적 SQL 태그가
                // 제거되어 깨진 SQL이 만들어진다.
                //
                // <foreach>: VALUES 반복이 단 1건으로 축소되고 item.Property 바인딩 실패
                // <where><if>: WHERE 키워드 없이 AND로 시작하는 구문 오류 SQL 생성
                //
                // 해결: 각 동적 태그를 __*_PLACEHOLDER__로 대체하고
                // 대응 인터셉터(BeforeExecute)에서 런타임에 실제 SQL로 확장한다.
                var foreachEl = element.Elements().FirstOrDefault(e => e.Name.LocalName == "foreach");
                var whereEl   = element.Elements().FirstOrDefault(e => e.Name.LocalName == "where");
                string sqlSource;

                if (foreachEl != null)
                {
                    // <foreach> 이전/이후 텍스트를 추출하고 플레이스홀더를 삽입한다.
                    var beforeForeach = new StringBuilder();
                    var afterForeach  = new StringBuilder();
                    bool pastForeach  = false;

                    foreach (var node in element.Nodes())
                    {
                        if (node is XText textNode)
                        {
                            if (!pastForeach) beforeForeach.Append(textNode.Value);
                            else             afterForeach.Append(textNode.Value);
                        }
                        else if (node is XElement childEl && childEl.Name.LocalName == "foreach")
                        {
                            pastForeach = true;
                        }
                    }

                    sqlSource = beforeForeach.ToString().TrimEnd()
                                + " __FOREACH_PLACEHOLDER__ "
                                + afterForeach.ToString().TrimStart();

                    ForeachTemplates[fullId] = new ForeachTemplateInfo
                    {
                        CollectionName = foreachEl.Attribute("collection")?.Value ?? "list",
                        ItemName       = foreachEl.Attribute("item")?.Value       ?? "item",
                        Separator      = foreachEl.Attribute("separator")?.Value  ?? ",",
                        BodyTemplate   = foreachEl.Value.Trim()
                    };
                }
                else if (whereEl != null)
                {
                    // <where> 이전/이후 텍스트를 추출하고 플레이스홀더를 삽입한다.
                    // <where> 내부의 <if> 조건들을 test/fragment 쌍으로 저장한다.
                    var beforeWhere = new StringBuilder();
                    var afterWhere  = new StringBuilder();
                    bool pastWhere  = false;

                    foreach (var node in element.Nodes())
                    {
                        if (node is XText textNode)
                        {
                            if (!pastWhere) beforeWhere.Append(textNode.Value);
                            else           afterWhere.Append(textNode.Value);
                        }
                        else if (node is XElement childEl && childEl.Name.LocalName == "where")
                        {
                            pastWhere = true;
                        }
                    }

                    sqlSource = beforeWhere.ToString().TrimEnd()
                                + " __WHERE_PLACEHOLDER__ "
                                + afterWhere.ToString().TrimStart();

                    // <where> 내부의 <if test="..."> 조건들을 순서대로 추출한다.
                    var conditions = whereEl.Elements()
                        .Where(e => e.Name.LocalName == "if")
                        .Select(ifEl => new WhereCondition
                        {
                            TestExpression = ifEl.Attribute("test")?.Value ?? "false",
                            SqlFragment    = ifEl.Value.Trim()
                        })
                        .ToList();

                    WhereTemplates[fullId] = new WhereTemplateInfo
                    {
                        Conditions = conditions
                    };
                }
                else
                {
                    // 노드별 순회로 <include refid="..."/>를 프래그먼트 텍스트로 치환한다.
                    // element.Value.Trim()은 XElement 자식의 텍스트만 수집하여
                    // <include> 같은 자식 요소의 기여가 사라진다.
                    var sb = new StringBuilder();
                    foreach (var node in element.Nodes())
                    {
                        if (node is XText textNode)
                            sb.Append(textNode.Value);
                        else if (node is XElement childEl && childEl.Name.LocalName == "include")
                        {
                            var refId = childEl.Attribute("refid")?.Value ?? "";
                            if (sqlFragments.TryGetValue(refId, out var fragment))
                                sb.Append(fragment);
                        }
                    }
                    sqlSource = sb.ToString().Trim();
                }

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
