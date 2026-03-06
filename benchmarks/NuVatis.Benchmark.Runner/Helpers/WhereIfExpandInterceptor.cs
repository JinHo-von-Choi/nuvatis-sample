using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using NuVatis.Interceptor;

namespace NuVatis.Benchmark.Runner.Helpers;

/**
 * NuVatis <where><if> 동적 SQL 런타임 확장 인터셉터
 *
 * 【 존재 이유 】
 * DatabaseInitializer.LoadXmlMappers()는 XElement.Value.Trim()으로 SQL을 추출한다.
 * XElement.Value는 모든 자식 텍스트 노드를 연결하므로, <where><if> 태그가 제거되어
 * 각 조건 절이 WHERE 없이 AND로 시작하는 깨진 SQL이 만들어진다.
 *
 * 그 결과 XML:
 *   FROM users
 *   <where>
 *     <if test="UserName != null">AND user_name LIKE #{UserName}</if>
 *     <if test="Email != null">AND email LIKE #{Email}</if>
 *   </where>
 *
 * 가 SqlSource로 저장될 때:
 *   FROM users AND user_name LIKE #{UserName} AND email LIKE #{Email}
 *
 * 로 저장된다. WHERE 키워드도 없고, 첫 AND도 제거되지 않아 PostgreSQL 42601 오류 발생.
 *
 * 【 수정 방법 】
 * 1. LoadXmlMappers가 <where>를 감지하면 SqlSource에 __WHERE_PLACEHOLDER__를 삽입하고
 *    WhereTemplates 딕셔너리에 조건 목록을 저장한다.
 *
 * 2. ParameterBinder.Bind()는 #{...} 패턴만 처리하므로
 *    __WHERE_PLACEHOLDER__를 그대로 통과시킨다.
 *
 * 3. BeforeExecute에서 플레이스홀더를 감지하면:
 *    - ctx.Parameter에서 각 조건의 test 표현식을 평가한다
 *    - 참인 조건의 SQL 프래그먼트를 모아 WHERE 절을 구성한다
 *    - 모든 조건이 거짓이면 WHERE 절을 완전히 제거한다
 *    - ctx.Sql을 완성된 SQL로 교체한다
 *
 * 【 test 표현식 지원 패턴 】
 * - "Prop != null"        → Prop 프로퍼티가 null이 아닐 때 조건 포함
 * - "Prop != ''"         → Prop이 null도 아니고 빈 문자열도 아닐 때 포함
 * - "Prop == null"        → Prop이 null일 때 조건 포함
 * - 복합 조건: "Prop1 != null and Prop2 != null" (and/AND 구분자로 연결)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-06
 */
public sealed class WhereIfExpandInterceptor : ISqlInterceptor
{
    private const string Placeholder = "__WHERE_PLACEHOLDER__";

    private static readonly Regex ParamPattern = new(@"#\{(\w+(?:\.\w+)*)\}", RegexOptions.Compiled);

    /**
     * 실행 전 훅: <where><if> 플레이스홀더를 실제 WHERE 절로 확장한다.
     *
     * 플레이스홀더가 없거나 템플릿 정보가 없으면 즉시 반환한다.
     * 이 인터셉터는 모든 SQL 실행에 등록되므로 불필요한 오버헤드를
     * 최소화하는 빠른 경로(fast path)가 중요하다.
     */
    public void BeforeExecute(InterceptorContext ctx)
    {
        if (!ctx.Sql.Contains(Placeholder)) return;

        if (!DatabaseInitializer.WhereTemplates.TryGetValue(ctx.StatementId, out var template)) return;

        var newParams = new List<DbParameter>(ctx.Parameters);
        int pIdx = newParams.Count;
        var sb = new StringBuilder();

        foreach (var condition in template.Conditions)
        {
            if (!EvaluateTest(condition.TestExpression, ctx.Parameter)) continue;

            var fragment = condition.SqlFragment.Trim();
            if (sb.Length == 0)
            {
                // 첫 번째 조건: 앞의 AND / OR 제거 (MyBatis <where> 태그 동작과 동일)
                if (fragment.StartsWith("AND ", StringComparison.OrdinalIgnoreCase))
                    fragment = fragment[4..].TrimStart();
                else if (fragment.StartsWith("OR ", StringComparison.OrdinalIgnoreCase))
                    fragment = fragment[3..].TrimStart();
            }

            // #{Prop} → @pN 파라미터 바인딩 (ForeachExpandInterceptor와 동일한 패턴)
            var pIdxArr = new int[] { pIdx };
            fragment = ParamPattern.Replace(fragment, m =>
            {
                var propName = m.Groups[1].Value;
                var value    = ctx.Parameter != null ? GetPropertyValue(ctx.Parameter, propName) : null;
                var name     = $"@p{pIdxArr[0]++}";
                newParams.Add(new SimpleDbParameter(name, value ?? DBNull.Value));
                return name;
            });
            pIdx = pIdxArr[0];

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(fragment);
        }

        var whereClause = sb.Length > 0
            ? "WHERE " + sb
            : string.Empty;

        ctx.Sql        = ctx.Sql.Replace(Placeholder, whereClause);
        ctx.Parameters = newParams;
    }

    /**
     * 실행 후 훅: 이 인터셉터는 사후 처리가 없다.
     */
    public void AfterExecute(InterceptorContext ctx) { }

    /**
     * 비동기 실행 전 훅: 동기 버전과 동일한 처리를 수행한다.
     * 조건 평가는 순수 메모리 연산이므로 별도 비동기 처리가 필요하지 않다.
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
     * test 표현식 문자열을 평가하여 조건 포함 여부를 반환한다.
     *
     * 복합 조건은 " and " 또는 " AND "로 연결하며, 모든 조건이 참이어야 한다.
     * 예: "UserName != null and UserName != ''"
     */
    private static bool EvaluateTest(string testExpr, object? parameter)
    {
        if (parameter == null) return false;

        var parts = testExpr.Split(new[] { " and ", " AND " }, StringSplitOptions.RemoveEmptyEntries);
        return parts.All(part => EvaluateSingleCondition(part.Trim(), parameter));
    }

    /**
     * 단일 조건 절을 평가한다.
     *
     * 지원 패턴:
     * - "Prop != null"  → 프로퍼티가 null이 아닐 때 true
     * - "Prop == null"  → 프로퍼티가 null일 때 true
     * - "Prop != ''"   → 프로퍼티가 null도 빈 문자열도 아닐 때 true
     * - "Prop == ''"   → 프로퍼티가 null이거나 빈 문자열일 때 true
     */
    private static bool EvaluateSingleCondition(string condition, object parameter)
    {
        // "Prop != null"
        var neqNull = Regex.Match(condition, @"^(\w+)\s*!=\s*null$", RegexOptions.IgnoreCase);
        if (neqNull.Success)
        {
            var value = GetPropertyValue(parameter, neqNull.Groups[1].Value);
            return value != null;
        }

        // "Prop == null"
        var eqNull = Regex.Match(condition, @"^(\w+)\s*==\s*null$", RegexOptions.IgnoreCase);
        if (eqNull.Success)
        {
            var value = GetPropertyValue(parameter, eqNull.Groups[1].Value);
            return value == null;
        }

        // "Prop != ''"
        var neqEmpty = Regex.Match(condition, @"^(\w+)\s*!=\s*''$");
        if (neqEmpty.Success)
        {
            var value = GetPropertyValue(parameter, neqEmpty.Groups[1].Value);
            return value != null && value.ToString() != string.Empty;
        }

        // "Prop == ''"
        var eqEmpty = Regex.Match(condition, @"^(\w+)\s*==\s*''$");
        if (eqEmpty.Success)
        {
            var value = GetPropertyValue(parameter, eqEmpty.Groups[1].Value);
            return value == null || value.ToString() == string.Empty;
        }

        // 알 수 없는 패턴: 포함하지 않음 (안전한 기본값)
        return false;
    }

    /**
     * 파라미터 객체에서 단일 프로퍼티 값을 반사(Reflection)로 조회한다.
     *
     * 프로퍼티가 없거나 값을 읽을 수 없으면 null을 반환한다.
     */
    private static object? GetPropertyValue(object parameter, string propertyName)
    {
        var prop = parameter.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        return prop?.GetValue(parameter);
    }

    /**
     * ParameterBinder 내부의 GenericDbParameter와 동일한 경량 구현체.
     * ForeachExpandInterceptor.SimpleDbParameter와 동일한 패턴.
     */
    private sealed class SimpleDbParameter : DbParameter
    {
        public override DbType             DbType                  { get; set; }
        public override ParameterDirection Direction               { get; set; } = ParameterDirection.Input;
        public override bool               IsNullable              { get; set; } = true;
        public override string             ParameterName           { get; set; }
        public override int                Size                    { get; set; }
        public override string             SourceColumn            { get; set; } = string.Empty;
        public override bool               SourceColumnNullMapping { get; set; }
        public override object?            Value                   { get; set; }

        public SimpleDbParameter(string name, object? value)
        {
            ParameterName = name;
            Value         = value;
        }

        public override void ResetDbType() { }
    }
}
