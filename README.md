# NuVatis 샘플 프로젝트

[![NuVatis Version](https://img.shields.io/badge/NuVatis-2.1.0-blue.svg)](https://www.nuget.org/packages/NuVatis.Core)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**NuVatis 2.1.0**을 실증적으로 학습할 수 있는 종합 샘플 프로젝트입니다.

MyBatis 스타일의 XML 매퍼, 동적 SQL, ResultMap(association/collection), 트랜잭션, ASP.NET Core 통합, 그리고 **Dapper, EF Core와의 대규모 성능 비교**까지 모든 것을 다룹니다.

**작성자:** 최진호
**작성일:** 2026-03-01
**라이센스:** MIT

---

## 📋 목차

- [프로젝트 소개](#-프로젝트-소개)
- [주요 기능](#-주요-기능)
- [프로젝트 구조](#-프로젝트-구조)
- [시작하기](#️-시작하기)
- [사용 예제](#-사용-예제)
- [API 엔드포인트](#-api-엔드포인트)
- [벤치마크 결과](#-벤치마크-결과)
- [개발 가이드](#-개발-가이드)
- [트러블슈팅](#-트러블슈팅)
- [라이선스](#-라이선스)

---

## 📖 프로젝트 소개

NuVatis는 **.NET을 위한 MyBatis 스타일 SQL 매퍼 프레임워크**입니다.

이 샘플 프로젝트는 초보자부터 전문가까지 NuVatis의 모든 기능을 학습할 수 있도록 **상세한 주석**과 **실무 패턴**을 포함합니다.

### 💡 학습 목표

1. **XML 매퍼 방식**: MyBatis 동적 SQL (`<if>`, `<foreach>`, `<where>`)
2. **복잡한 ResultMap**: `association` (1:1), `collection` (1:N), `nested association`
3. **트랜잭션 처리**: 주문 생성 + 재고 차감 원자적 처리
4. **동시성 제어**: 재고 업데이트 경쟁 조건 해결 (원자적 업데이트)
5. **ASP.NET Core 통합**: Dependency Injection, Health Check
6. **성능 비교**: NuVatis vs Dapper vs EF Core 실증 벤치마크

### 🎯 이 프로젝트가 특별한 이유

- **상세한 주석**: 모든 XML 매퍼, Controller, Model에 500-1000줄의 교육용 주석
- **실무 패턴**: Soft Delete, 가격 스냅샷, 주문 상태 FSM, 재고 동시성 제어
- **대규모 벤치마크**: 70GB 데이터, 18개 시나리오, 3개 ORM 비교
- **즉시 실행 가능**: Docker Compose로 1분 안에 실행

---

## 🚀 주요 기능

### 1. XML 매퍼 방식

**IUserMapper.xml** - 사용자 CRUD + 동적 검색
- 동적 SQL: `<where>`, `<if>`, `<foreach>`
- Soft Delete vs Hard Delete
- 페이징 (OFFSET/LIMIT)
- N+1 문제 해결

**IOrderMapper.xml** - 복잡한 JOIN 쿼리
- `association`: Order → User (1:1)
- `collection`: Order → OrderItem[] (1:N)
- `nested association`: OrderItem → Product (중첩)

**IProductMapper.xml** - 재고 관리
- 원자적 재고 업데이트 (동시성 안전)
- Read-Modify-Write 문제 해결

### 2. 동적 SQL

```xml
<select id="Search" resultMap="UserResult">
  SELECT * FROM users
  <where>
    <if test="UserName != null">
      AND user_name LIKE '%' || #{UserName} || '%'
    </if>
    <if test="Ids != null and Ids.Count > 0">
      AND id IN
      <foreach collection="Ids" item="id" open="(" separator="," close=")">
        #{id}
      </foreach>
    </if>
  </where>
</select>
```

### 3. ResultMap (복잡한 객체 매핑)

```xml
<resultMap id="OrderWithItemsResult" type="Order">
  <id column="id" property="Id" />

  <!-- association: 1:1 관계 -->
  <association property="User" javaType="User">
    <id column="user_id" property="Id" />
    <result column="user_name" property="UserName" />
  </association>

  <!-- collection: 1:N 관계 -->
  <collection property="Items" ofType="OrderItem">
    <id column="item_id" property="Id" />
    <result column="item_quantity" property="Quantity" />

    <!-- nested association: 중첩 관계 -->
    <association property="Product" javaType="Product">
      <id column="product_id" property="Id" />
      <result column="product_name" property="ProductName" />
    </association>
  </collection>
</resultMap>
```

### 4. 원자적 재고 업데이트 (동시성 제어)

```xml
<update id="UpdateStock">
  UPDATE products
  SET stock_qty = stock_qty + #{Quantity}
  WHERE id = #{ProductId}
</update>
```

**왜 원자적 업데이트?**
- Read-Modify-Write 패턴은 경쟁 조건 발생
- DB가 원자적으로 처리하여 동시성 안전

---

## 📁 프로젝트 구조

```
nuvatis-sample/
├── src/
│   ├── NuVatis.Sample.Core/              # 공통 라이브러리
│   │   ├── Models/                       # 엔티티 (극도로 상세한 주석)
│   │   │   ├── User.cs
│   │   │   ├── Product.cs
│   │   │   ├── Order.cs
│   │   │   ├── OrderItem.cs
│   │   │   └── UserSearchParam.cs
│   │   ├── Mappers/                      # 매퍼 인터페이스
│   │   │   ├── IUserMapper.cs
│   │   │   ├── IOrderMapper.cs
│   │   │   ├── IProductMapper.cs
│   │   │   └── Xml/                      # XML 매퍼 (극도로 상세한 주석)
│   │   │       ├── IUserMapper.xml       (~600줄 주석)
│   │   │       ├── IProductMapper.xml    (~500줄 주석)
│   │   │       └── IOrderMapper.xml      (~700줄 주석)
│   ├── NuVatis.Sample.Console/           # 콘솔 앱 예제
│   │   └── Program.cs
│   └── NuVatis.Sample.WebApi/            # ASP.NET Core Web API
│       ├── Controllers/                  # 극도로 상세한 주석
│       │   ├── UsersController.cs
│       │   ├── ProductsController.cs
│       │   └── OrdersController.cs
│       └── Program.cs
├── benchmarks/                           # 대규모 ORM 벤치마크
│   ├── NuVatis.Benchmark.Core/
│   ├── NuVatis.Benchmark.NuVatis/
│   ├── NuVatis.Benchmark.Dapper/
│   ├── NuVatis.Benchmark.EfCore/
│   ├── NuVatis.Benchmark.DataGen/
│   └── NuVatis.Benchmark.Runner/
├── database/
│   ├── schema.sql                        # PostgreSQL 스키마
│   └── seed.sql                          # 샘플 데이터
├── resources/
│   └── images/                           # 벤치마크 결과 이미지
├── docker-compose.yml
└── README.md
```

---

## 🛠️ 시작하기

### 1. 사전 요구사항

- **.NET 10 SDK** (또는 .NET 8+)
- **Docker** (PostgreSQL 실행용)
- (선택) **curl** 또는 **Postman** (API 테스트용)

### 2. 데이터베이스 실행

```bash
# Docker Compose로 PostgreSQL 실행
docker-compose up -d

# 상태 확인
docker-compose ps

# 로그 확인
docker-compose logs -f postgres
```

**연결 정보:**
- Host: `localhost`
- Port: `5432`
- Database: `nuvatis_sample`
- Username: `nuvatis`
- Password: `nuvatis123`

스키마와 샘플 데이터는 자동으로 초기화됩니다.

### 3. 프로젝트 빌드

```bash
# 솔루션 빌드
dotnet build

# 또는 특정 프로젝트만 빌드
dotnet build src/NuVatis.Sample.WebApi/NuVatis.Sample.WebApi.csproj
```

### 4. 실행

#### Web API 실행

```bash
cd src/NuVatis.Sample.WebApi
dotnet run
```

**접속:**
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

#### 콘솔 앱 실행

```bash
cd src/NuVatis.Sample.Console
dotnet run
```

---

## 💡 사용 예제

### 예제 1: 동적 검색 (XML 매퍼)

**C# 코드:**
```csharp
var param = new UserSearchParam
{
    UserName = "john",
    Email    = "example.com",
    IsActive = true,
    Ids      = new List<int> { 1, 2, 3 },
    Offset   = 0,
    Limit    = 10
};

var users = _userMapper.Search(param);
```

**생성되는 SQL:**
```sql
SELECT id, user_name, email, full_name, created_at, updated_at, is_active
FROM users
WHERE user_name LIKE '%john%'
  AND email LIKE '%example.com%'
  AND is_active = true
  AND id IN (1, 2, 3)
ORDER BY created_at DESC
LIMIT 10 OFFSET 0
```

### 예제 2: 원자적 재고 업데이트

**잘못된 방법 (경쟁 조건):**
```csharp
var product = _productMapper.GetById(1);
product.StockQty -= 5;  // 위험! 동시 요청 시 재고 부정확
_productMapper.Update(product);
```

**올바른 방법 (원자적 업데이트):**
```csharp
_productMapper.UpdateStock(productId, -5);  // 안전! DB가 원자적 처리
```

**SQL:**
```sql
UPDATE products
SET stock_qty = stock_qty - 5
WHERE id = 1
```

### 예제 3: 복잡한 JOIN (association + collection)

**C# 코드:**
```csharp
var order = _orderMapper.GetByIdWithItems(123);

Console.WriteLine($"주문번호: {order.OrderNo}");
Console.WriteLine($"주문자: {order.User.FullName}");  // association

foreach (var item in order.Items)  // collection
{
    Console.WriteLine($"  - {item.Product.ProductName} x {item.Quantity}");  // nested
}
```

**출력:**
```
주문번호: ORD-20260301-0001
주문자: 홍길동
  - 삼성 노트북 x 1
  - 로지텍 마우스 x 2
```

**생성되는 SQL:**
```sql
SELECT
  o.id, o.order_no,
  u.user_name, u.full_name,
  oi.id AS item_id, oi.quantity,
  p.product_name
FROM orders o
INNER JOIN users u ON o.user_id = u.id
LEFT JOIN order_items oi ON o.id = oi.order_id
LEFT JOIN products p ON oi.product_id = p.id
WHERE o.id = 123
```

---

## 🌐 API 엔드포인트

### Users API

| Method | Endpoint | 설명 |
|--------|----------|------|
| GET | `/api/users` | 모든 사용자 조회 |
| GET | `/api/users/{id}` | ID로 사용자 조회 |
| GET | `/api/users/search` | 동적 검색 (userName, email, isActive, ids, offset, limit) |
| POST | `/api/users` | 사용자 등록 |
| PUT | `/api/users/{id}` | 사용자 수정 |
| DELETE | `/api/users/{id}` | 사용자 삭제 (Soft Delete) |

**검색 예제:**
```bash
curl "http://localhost:5000/api/users/search?userName=john&isActive=true&limit=10"
```

### Products API

| Method | Endpoint | 설명 |
|--------|----------|------|
| GET | `/api/products` | 모든 상품 조회 |
| GET | `/api/products/{id}` | ID로 상품 조회 |
| GET | `/api/products/category/{category}` | 카테고리별 조회 |
| POST | `/api/products` | 상품 등록 |
| PUT | `/api/products/{id}` | 상품 수정 |
| PATCH | `/api/products/{id}/stock` | 재고 업데이트 (원자적) |
| DELETE | `/api/products/{id}` | 상품 삭제 |

**재고 업데이트 예제:**
```bash
curl -X PATCH http://localhost:5000/api/products/1/stock \
  -H "Content-Type: application/json" \
  -d '{"quantity": -5}'
```

### Orders API

| Method | Endpoint | 설명 |
|--------|----------|------|
| GET | `/api/orders/{id}` | 주문 조회 (User 포함) |
| GET | `/api/orders/{id}/with-items` | 주문 상세 조회 (Items + Product 포함) |
| GET | `/api/orders/user/{userId}` | 사용자별 주문 목록 |
| POST | `/api/orders` | 주문 생성 |
| PUT | `/api/orders/{id}/status` | 주문 상태 업데이트 |
| DELETE | `/api/orders/{id}` | 주문 삭제 |

**주문 생성 예제:**
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1,
    "items": [
      {"productId": 1, "quantity": 2},
      {"productId": 2, "quantity": 1}
    ]
  }'
```

---

## 📈 벤치마크 결과

### 🎯 벤치마크 개요

**NuVatis vs Dapper vs EF Core** 대규모 성능 비교

- **데이터 규모**: ~70GB (100K users, 10M orders, 50M order_items 등 15개 테이블)
- **시나리오**: 60개 이상 (Simple CRUD, WHERE/JOIN, 동적 SQL, 집계, 대량 작업, 스트레스 테스트 등)
- **측정 지표**: Latency (Mean, P95, P99), Throughput (ops/sec), Memory (GC, Allocation)
- **환경**: PostgreSQL 14, .NET 8.0, Docker

### 📊 벤치마크 결과 상세

#### 1. Simple CRUD - GetById (PK 조회)

![GetById Benchmark](resources/images/스크린샷%202026-03-01%20072900.png)

**결과 분석:**
- **NuVatis**: ~0.8ms (빠름, XML 파싱 오버헤드 최소)
- **Dapper**: ~0.5ms (가장 빠름, 최소 추상화)
- **EF Core**: ~1.2ms (느림, Change Tracking 오버헤드)

**결론**: 단순 PK 조회에서는 Dapper > NuVatis > EF Core

---

#### 2. WHERE Clause 검색

![WhereClause Benchmark](resources/images/스크린샷%202026-03-01%20072909.png)

**결과 분석:**
- **NuVatis**: ~3ms (동적 SQL 효율적)
- **Dapper**: ~2.5ms (직접 SQL, 가장 빠름)
- **EF Core**: ~5ms (LINQ 번역 오버헤드)

**결론**: 조건 검색에서도 Dapper가 가장 빠르지만, NuVatis도 경쟁력 있음

---

#### 3. 페이징 (OFFSET/LIMIT)

![Paging Benchmark](resources/images/스크린샷%202026-03-01%20072920.png)

**결과 분석:**
- **NuVatis**: ~5ms (안정적)
- **Dapper**: ~4ms (빠름)
- **EF Core**: ~8ms (Skip/Take 비효율)

**결론**: 페이징에서 EF Core가 불리 (OFFSET/LIMIT 직접 사용 권장)

---

#### 4. JOIN 쿼리 (2-3개 테이블)

![JOIN Benchmark](resources/images/스크린샷%202026-03-01%20072927.png)

**결과 분석:**
- **NuVatis**: ~12ms (ResultMap 효율적)
- **Dapper**: ~10ms (수동 매핑, 가장 빠름)
- **EF Core**: ~25ms (Include 오버헤드)

**결론**: JOIN에서 NuVatis의 ResultMap이 EF Core Include보다 2배 빠름

---

#### 5. 복잡한 JOIN (5개 이상 테이블)

![Complex JOIN Benchmark](resources/images/스크린샷%202026-03-01%20072933.png)

**결과 분석:**
- **NuVatis**: ~30ms (collection + nested association)
- **Dapper**: ~25ms (수동 그룹화 필요)
- **EF Core**: ~60ms (다중 Include 비효율)

**결론**: 복잡한 JOIN에서 NuVatis가 EF Core보다 2배 빠름, Dapper와 경쟁력

---

#### 6. 동적 SQL (MyBatis 스타일)

![Dynamic SQL Benchmark](resources/images/스크린샷%202026-03-01%20072938.png)

**결과 분석:**
- **NuVatis**: ~8ms (XML `<if>`, `<foreach>` 효율적)
- **Dapper**: ~6ms (수동 SQL 조합 필요)
- **EF Core**: ~15ms (동적 Where 비효율)

**결론**: NuVatis의 동적 SQL이 EF Core보다 거의 2배 빠름

---

#### 7. GROUP BY + Aggregate

![Aggregate Benchmark](resources/images/스크린샷%202026-03-01%20072944.png)

**결과 분석:**
- **NuVatis**: ~20ms
- **Dapper**: ~18ms
- **EF Core**: ~35ms (GroupBy 번역 비효율)

**결론**: 집계 쿼리에서 EF Core가 가장 느림

---

#### 8. Bulk Insert (1,000건)

![Bulk Insert Benchmark](resources/images/스크린샷%202026-03-01%20072950.png)

**결과 분석:**
- **NuVatis**: ~150ms
- **Dapper**: ~120ms (일괄 INSERT 효율적)
- **EF Core**: ~300ms (AddRange 비효율)

**결론**: 대량 INSERT에서 EF Core가 2배 느림

---

#### 9. Memory Allocation (메모리 사용량)

![Memory Benchmark](resources/images/스크린샷%202026-03-01%20072958.png)

**결과 분석:**
- **NuVatis**: ~50KB / 쿼리 (중간)
- **Dapper**: ~30KB / 쿼리 (가장 적음)
- **EF Core**: ~120KB / 쿼리 (가장 많음, Change Tracking)

**결론**: 메모리 효율성 Dapper > NuVatis > EF Core

---

### 🏆 종합 결론

#### NuVatis 특성
**강점:**
- 복잡한 동적 SQL (Dapper 대비 코드 간결)
- ResultMap의 강력한 매핑 (EF Core Include보다 빠름)
- XML로 SQL 관리 (버전 관리, 재사용)

**약점:**
- 단순 쿼리에서 Dapper보다 약간 느림
- 컴파일 타임 체크 부재 (XML)

#### Dapper 특성
**강점:**
- 모든 시나리오에서 가장 빠름
- 최소 메모리 사용
- 단순 명확

**약점:**
- 동적 SQL 수동 구성 (보일러플레이트)
- 복잡한 매핑 수동 처리
- N+1 문제 수동 해결

#### EF Core 특성
**강점:**
- LINQ 타입 안전성
- Change Tracking (업데이트 편리)
- 마이그레이션 자동화

**약점:**
- 복잡 쿼리 비효율 (2배 느림)
- 높은 메모리 사용 (4배)
- AsNoTracking 필수

### 📌 권장 사용 시나리오

| 시나리오 | 권장 ORM |
|---------|---------|
| 단순 CRUD | Dapper |
| 복잡한 동적 검색 | **NuVatis** |
| 복잡한 JOIN + 매핑 | **NuVatis** |
| 대량 데이터 처리 | Dapper |
| 도메인 모델 중심 | EF Core |
| 레거시 DB 통합 | **NuVatis** |

---

## 📚 개발 가이드

### NuVatis 주요 개념

#### 1. XML 매퍼 기본 구조

```xml
<mapper namespace="NuVatis.Sample.Core.Mappers.IUserMapper">
  <!-- ResultMap: 컬럼 → 속성 매핑 -->
  <resultMap id="UserResult" type="User">
    <id column="id" property="Id" />
    <result column="user_name" property="UserName" />
  </resultMap>

  <!-- SELECT 쿼리 -->
  <select id="GetById" resultMap="UserResult">
    SELECT * FROM users WHERE id = #{Id}
  </select>

  <!-- INSERT 쿼리 -->
  <insert id="Insert">
    INSERT INTO users (user_name, email) VALUES (#{UserName}, #{Email})
  </insert>
</mapper>
```

#### 2. 동적 SQL 태그

```xml
<where>
  <if test="UserName != null">
    AND user_name LIKE '%' || #{UserName} || '%'
  </if>
  <if test="Ids != null and Ids.Count > 0">
    AND id IN
    <foreach collection="Ids" item="id" open="(" separator="," close=")">
      #{id}
    </foreach>
  </if>
</where>
```

#### 3. association vs collection

```xml
<!-- association: 1:1 관계 -->
<association property="User" javaType="User">
  <id column="user_id" property="Id" />
  <result column="user_name" property="UserName" />
</association>

<!-- collection: 1:N 관계 -->
<collection property="Items" ofType="OrderItem">
  <id column="item_id" property="Id" />
  <result column="quantity" property="Quantity" />
</collection>
```

### 코드 예제

#### DI 설정 (ASP.NET Core)

```csharp
builder.Services.AddScoped<IUserMapper, IUserMapper>();
builder.Services.AddScoped<IProductMapper, IProductMapper>();
builder.Services.AddScoped<IOrderMapper, IOrderMapper>();
```

#### 매퍼 사용

```csharp
public class UsersController : ControllerBase
{
    private readonly IUserMapper _userMapper;

    public UsersController(IUserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(int id)
    {
        var user = await _userMapper.GetByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }
}
```

---

## 🔧 트러블슈팅

### 문제: "테이블을 찾을 수 없음"

**해결:** Docker Compose 재시작
```bash
docker-compose down -v
docker-compose up -d
```

### 문제: XML 파일을 찾을 수 없음

**해결:** .csproj에 AdditionalFiles 추가 확인
```xml
<ItemGroup>
  <AdditionalFiles Include="Mappers\Xml\*.xml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </AdditionalFiles>
</ItemGroup>
```

### 문제: Connection refused

**해결:** PostgreSQL 상태 확인
```bash
docker-compose ps
docker-compose logs postgres
```

### 문제: 벤치마크 결과 불안정

**해결:**
```bash
# 백그라운드 프로세스 종료
# Warmup 증가 (BenchmarkDotNet 설정)
# Release 모드 실행 확인
dotnet run -c Release
```

---

## 📄 라이선스

MIT License - 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

---

## 📞 문의

- **작성자:** 최진호
- **이메일:** jinho.von.choi@nerdvana.kr
- **NuVatis GitHub:** https://github.com/JinHo-von-Choi/nuvatis
- **NuGet:** https://www.nuget.org/packages/NuVatis.Core

---

## 🎓 학습 가이드

이 프로젝트의 **극도로 상세한 주석**을 순서대로 학습하세요:

1. **Models** (데이터베이스 매핑 이해)
   - `User.cs` - 기본 엔티티
   - `Product.cs` - 재고 관리
   - `Order.cs` - association/collection
   - `OrderItem.cs` - 가격 스냅샷

2. **XML 매퍼** (SQL 작성 방법)
   - `IUserMapper.xml` - 동적 SQL, 페이징
   - `IProductMapper.xml` - 원자적 업데이트
   - `IOrderMapper.xml` - 복잡한 JOIN

3. **Controllers** (실무 패턴)
   - `UsersController.cs` - RESTful API
   - `ProductsController.cs` - 동시성 제어
   - `OrdersController.cs` - 트랜잭션

4. **벤치마크** (성능 이해)
   - `benchmarks/` 디렉토리 전체

---

**⭐ 이 프로젝트가 도움이 되었다면 Star를 눌러주세요!**

---

**Sources:**
- [GitHub - JinHo-von-Choi/nuvatis](https://github.com/JinHo-von-Choi/nuvatis)
- [NuGet - NuVatis.Core](https://www.nuget.org/packages/NuVatis.Core)
