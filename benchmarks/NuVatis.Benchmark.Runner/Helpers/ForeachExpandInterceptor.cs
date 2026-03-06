using System.Collections;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using NuVatis.Interceptor;

namespace NuVatis.Benchmark.Runner.Helpers;

/**
 * NuVatis <foreach> 동적 SQL 런타임 확장 인터셉터
 *
 * 【 존재 이유 】
 * DatabaseInitializer.LoadXmlMappers()는 XElement.Value.Trim()으로 SQL을 추출한다.
 * XElement.Value는 모든 자식 텍스트 노드를 연결하므로, <foreach> 태그는 제거되고
 * 내부 바디만 단 한 번 남게 된다.
 *
 * 그 결과 XML:
 *   VALUES
 *   <foreach collection="users" item="user" separator=",">
 *     (#{user.UserName}, ...)
 *   </foreach>
 *
 * 가 SqlSource로 저장될 때:
 *   VALUES (#{user.UserName}, ...)  ← 1개의 VALUES 튜플, 반복 없음
 *
 * 로 저장된다. 더불어 #{user.UserName}에서 "user"를 IEnumerable<User> 객체의 프로퍼티로
 * 찾으므로 null → NOT NULL 제약 위반(23502) 발생.
 *
 * 【 수정 방법 】
 * 1. LoadXmlMappers가 <foreach>를 감지하면 SqlSource에 __FOREACH_PLACEHOLDER__를 삽입하고
 *    ForeachTemplates 딕셔너리에 템플릿 정보를 저장한다.
 *
 * 2. ParameterBinder.Bind()는 #{...} 패턴만 처리하므로
 *    __FOREACH_PLACEHOLDER__를 그대로 통과시킨다.
 *
 * 3. BeforeExecute에서 플레이스홀더를 감지하면:
 *    - ctx.Parameter에서 컬렉션을 추출한다
 *    - 컬렉션을 이터레이션하며 바디 템플릿을 N번 확장한다
 *    - #{item.Property} 패턴을 @p0, @p1... 파라미터로 치환한다
 *    - ctx.Sql과 ctx.Parameters를 확장된 버전으로 교체한다
 *
 * 4. SimpleExecutor.ExecuteAsync가 확장된 SQL과 파라미터로 실행한다.
 *
 * 작성자: 최진호
 * 작성일: 2026-03-06
 */
public sealed class ForeachExpandInterceptor : ISqlInterceptor
{
    private const string Placeholder = "__FOREACH_PLACEHOLDER__";

    private static readonly Regex ParamPattern = new(@"#\{(\w+(?:\.\w+)*)\}", RegexOptions.Compiled);

    private static readonly System.Reflection.BindingFlags PublicInstance =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

    /**
     * 실행 전 훅: foreach 플레이스홀더를 실제 SQL로 확장한다.
     *
     * 플레이스홀더가 없거나 템플릿 정보가 없으면 즉시 반환한다.
     * 이 인터셉터는 모든 SQL 실행에 등록되므로 불필요한 오버헤드를
     * 최소화하는 빠른 경로(fast path)가 중요하다.
     */
    public void BeforeExecute(InterceptorContext ctx)
    {
        if (!ctx.Sql.Contains(Placeholder)) return;

        if (!DatabaseInitializer.ForeachTemplates.TryGetValue(ctx.StatementId, out var template)) return;

        var collection = ResolveCollection(ctx.Parameter, template.CollectionName);
        if (collection == null) return;

        var sb = new StringBuilder();
        var parameters = new List<DbParameter>();
        bool first = true;
        int pIdx = 0;

        foreach (var item in collection)
        {
            if (!first) sb.Append(template.Separator);
            first = false;

            var expanded = ExpandBody(template.BodyTemplate, item, template.ItemName, ref pIdx, parameters);
            sb.Append(expanded);
        }

        ctx.Sql        = ctx.Sql.Replace(Placeholder, sb.ToString());
        ctx.Parameters = parameters;
    }

    /**
     * 실행 후 훅: 이 인터셉터는 사후 처리가 없다.
     */
    public void AfterExecute(InterceptorContext ctx) { }

    /**
     * 비동기 실행 전 훅: 동기 버전과 동일한 처리를 수행한다.
     * 프로퍼티 바인딩은 순수 메모리 연산이므로 별도 비동기 처리가 필요하지 않다.
     */
    public Task BeforeExecuteAsync(InterceptorContext ctx, CancellationToken ct)
    {
        BeforeExecute(ctx);
        return Task.CompletedTask;
    }

    /**
     * 비동기 실행 후 훅: 사후 처리 없음.
     */
    public Task AfterExecuteAsync(InterceptorContext ctx, CancellationToken ct) => Task.CompletedTask;

    /**
     * 파라미터 객체에서 컬렉션을 추출한다.
     *
     * 1. 파라미터 자체가 IEnumerable이면 (string 제외) 그대로 반환.
     *    → BulkInsertAsync(IEnumerable<User> users)처럼 컬렉션을 직접 전달하는 경우.
     *
     * 2. 파라미터가 collectionName 프로퍼티를 가진 DTO이면 해당 프로퍼티를 반환.
     *    → BulkInsertRequest { IEnumerable<User> Users } 패턴.
     */
    private static IEnumerable? ResolveCollection(object? parameter, string collectionName)
    {
        if (parameter == null) return null;

        if (parameter is IEnumerable enumerable && parameter is not string)
            return enumerable;

        var prop = parameter.GetType().GetProperty(collectionName, PublicInstance);
        return prop?.GetValue(parameter) as IEnumerable;
    }

    /**
     * 바디 템플릿의 #{item.Property} 패턴을 @pN 파라미터로 치환한다.
     *
     * item 변수명 처리:
     * - #{user.UserName} + item="user" → "user." 접두사를 제거하고 UserName을 조회
     * - #{UserName} (접두사 없음) → 직접 UserName을 조회
     * - #{user.Address.City} → 중첩 프로퍼티도 지원
     */
    private static string ExpandBody(
        string bodyTemplate,
        object item,
        string itemName,
        ref int pIdx,
        List<DbParameter> parameters)
    {
        // C# 컴파일러는 람다 내부에서 ref 파라미터를 직접 캡처하지 못한다.
        // int[] 배열 래퍼로 힙에 올려 클로저가 참조를 유지하게 한다.
        var pIdxArr = new int[] { pIdx };

        var result = ParamPattern.Replace(bodyTemplate, m =>
        {
            var path = m.Groups[1].Value;

            var prefix = itemName + ".";
            var resolvePath = path.StartsWith(prefix, StringComparison.Ordinal)
                ? path[prefix.Length..]
                : path;

            var value     = ResolvePath(item, resolvePath);
            var paramName = $"@p{pIdxArr[0]++}";

            parameters.Add(new SimpleDbParameter(paramName, value ?? DBNull.Value));
            return paramName;
        });

        pIdx = pIdxArr[0];
        return result;
    }

    /**
     * 점(.) 구분자를 따라 중첩 프로퍼티를 재귀적으로 조회한다.
     * 중간에 null이 있으면 즉시 null을 반환한다.
     */
    private static object? ResolvePath(object? obj, string path)
    {
        if (obj == null) return null;

        var current = obj;
        foreach (var part in path.Split('.'))
        {
            if (current == null) return null;

            var prop = current.GetType().GetProperty(part, PublicInstance);
            if (prop == null) return null;

            current = prop.GetValue(current);
        }
        return current;
    }

    /**
     * ParameterBinder 내부의 GenericDbParameter와 동일한 경량 구현체.
     *
     * SimpleExecutor는 실행 시 CreateParameter()로 DB 네이티브 파라미터를 새로 생성하고
     * ParameterName과 Value만 복사하므로, 나머지 멤버는 최소 구현으로도 충분하다.
     */
    private sealed class SimpleDbParameter : DbParameter
    {
        public override DbType         DbType                { get; set; }
        public override ParameterDirection Direction         { get; set; } = ParameterDirection.Input;
        public override bool           IsNullable            { get; set; } = true;
        public override string         ParameterName         { get; set; }
        public override int            Size                  { get; set; }
        public override string         SourceColumn          { get; set; } = string.Empty;
        public override bool           SourceColumnNullMapping { get; set; }
        public override object?        Value                 { get; set; }

        public SimpleDbParameter(string name, object? value)
        {
            ParameterName = name;
            Value         = value;
        }

        public override void ResetDbType() { }
    }
}
