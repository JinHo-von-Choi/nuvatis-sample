using Npgsql;
using NpgsqlTypes;
using Spectre.Console;
using NuVatis.Benchmark.DataGen.Generators;

/**
 * 대규모 벤치마크 데이터 생성기
 * PostgreSQL COPY 프로토콜 사용 (초당 50K+ 레코드)
 *
 * 작성자: 최진호
 * 작성일: 2026-03-01
 */

const string connectionString = "Host=localhost;Port=5432;Database=benchmark;Username=nuvatis;Password=nuvatis123";

AnsiConsole.Write(new FigletText("NuVatis Benchmark").Centered().Color(Color.Cyan1));
AnsiConsole.MarkupLine("[bold cyan]대규모 ORM 벤치마크 데이터 생성기[/]");
AnsiConsole.MarkupLine("[dim]PostgreSQL COPY 프로토콜 사용[/]\n");

try
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    AnsiConsole.MarkupLine("[green]✓[/] 데이터베이스 연결 성공");

    // 1. Users (100K)
    await GenerateUsersAsync(conn, 100_000);

    // 2. Categories (500, 계층형)
    var categoryIds = await GenerateCategoriesAsync(conn, 50, 3);

    // 3. Products (50K)
    var productIds = await GenerateProductsAsync(conn, 50_000, categoryIds);

    // 4. Orders (10M)
    await GenerateOrdersAsync(conn, 10_000_000);

    AnsiConsole.MarkupLine("\n[bold green]✓ 모든 데이터 생성 완료![/]");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[bold red]✗ 오류 발생:[/] {ex.Message}");
    AnsiConsole.WriteException(ex);
}

static async Task GenerateUsersAsync(NpgsqlConnection conn, int count)
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync($"[yellow]사용자 {count:N0}명 생성 중...[/]", async ctx =>
        {
            var generator = new UserGenerator();
            var users = generator.Generate(count);

            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY users (user_name, email, full_name, password_hash, date_of_birth, phone_number, is_active, created_at, updated_at) FROM STDIN BINARY");

            var progress = 0;
            foreach (var user in users)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(user.UserName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(user.Email, NpgsqlDbType.Varchar);
                await writer.WriteAsync(user.FullName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(user.PasswordHash, NpgsqlDbType.Varchar);
                await writer.WriteAsync(user.DateOfBirth, NpgsqlDbType.Date);
                await writer.WriteAsync(user.PhoneNumber, NpgsqlDbType.Varchar);
                await writer.WriteAsync(user.IsActive, NpgsqlDbType.Boolean);
                await writer.WriteAsync(user.CreatedAt, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(user.UpdatedAt, NpgsqlDbType.Timestamp);

                if (++progress % 10000 == 0)
                {
                    ctx.Status($"[yellow]사용자 {progress:N0}/{count:N0} 생성 중...[/]");
                }
            }

            await writer.CompleteAsync();
        });

    AnsiConsole.MarkupLine($"[green]✓[/] 사용자 {count:N0}명 생성 완료");
}

static async Task<List<long>> GenerateCategoriesAsync(NpgsqlConnection conn, int rootCount, int maxDepth)
{
    return await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync($"[yellow]계층형 카테고리 생성 중...[/]", async ctx =>
        {
            var generator = new CategoryGenerator();
            var categories = generator.GenerateHierarchical(rootCount, maxDepth).ToList();

            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY categories (parent_id, category_name, description, display_order, is_active, created_at, updated_at) FROM STDIN BINARY");

            foreach (var category in categories)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(category.ParentId, NpgsqlDbType.Bigint);
                await writer.WriteAsync(category.CategoryName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(category.Description, NpgsqlDbType.Text);
                await writer.WriteAsync(category.DisplayOrder, NpgsqlDbType.Integer);
                await writer.WriteAsync(category.IsActive, NpgsqlDbType.Boolean);
                await writer.WriteAsync(category.CreatedAt, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(category.UpdatedAt, NpgsqlDbType.Timestamp);
            }

            await writer.CompleteAsync();

            AnsiConsole.MarkupLine($"[green]✓[/] 카테고리 {categories.Count:N0}개 생성 완료");

            // 생성된 카테고리 ID 조회
            var ids = await conn.CreateCommand().Also(cmd =>
            {
                cmd.CommandText = "SELECT id FROM categories ORDER BY id";
            }).ExecuteReaderAsync();

            var categoryIds = new List<long>();
            while (await ids.ReadAsync())
            {
                categoryIds.Add(ids.GetInt64(0));
            }

            return categoryIds;
        });
}

static async Task<List<long>> GenerateProductsAsync(NpgsqlConnection conn, int count, List<long> categoryIds)
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync($"[yellow]상품 {count:N0}개 생성 중...[/]", async ctx =>
        {
            var generator = new ProductGenerator(categoryIds);
            var products = generator.Generate(count);

            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY products (category_id, product_name, description, price, cost_price, stock_quantity, sku, is_active, created_at, updated_at) FROM STDIN BINARY");

            var progress = 0;
            foreach (var product in products)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(product.CategoryId, NpgsqlDbType.Bigint);
                await writer.WriteAsync(product.ProductName, NpgsqlDbType.Varchar);
                await writer.WriteAsync(product.Description, NpgsqlDbType.Text);
                await writer.WriteAsync(product.Price, NpgsqlDbType.Numeric);
                await writer.WriteAsync(product.CostPrice, NpgsqlDbType.Numeric);
                await writer.WriteAsync(product.StockQuantity, NpgsqlDbType.Integer);
                await writer.WriteAsync(product.Sku, NpgsqlDbType.Varchar);
                await writer.WriteAsync(product.IsActive, NpgsqlDbType.Boolean);
                await writer.WriteAsync(product.CreatedAt, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(product.UpdatedAt, NpgsqlDbType.Timestamp);

                if (++progress % 5000 == 0)
                {
                    ctx.Status($"[yellow]상품 {progress:N0}/{count:N0} 생성 중...[/]");
                }
            }

            await writer.CompleteAsync();
        });

    AnsiConsole.MarkupLine($"[green]✓[/] 상품 {count:N0}개 생성 완료");

    // 생성된 상품 ID 조회
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id FROM products ORDER BY id";
    var reader = await cmd.ExecuteReaderAsync();

    var productIds = new List<long>();
    while (await reader.ReadAsync())
    {
        productIds.Add(reader.GetInt64(0));
    }

    return productIds;
}

static async Task GenerateOrdersAsync(NpgsqlConnection conn, int count)
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync($"[yellow]주문 {count:N0}건 생성 중 (시간이 걸릴 수 있습니다)...[/]", async ctx =>
        {
            // 사용자 ID 조회
            var userIdsCmd = conn.CreateCommand();
            userIdsCmd.CommandText = "SELECT id FROM users ORDER BY id";
            var userIdsReader = await userIdsCmd.ExecuteReaderAsync();

            var userIds = new List<long>();
            while (await userIdsReader.ReadAsync())
            {
                userIds.Add(userIdsReader.GetInt64(0));
            }
            await userIdsReader.CloseAsync();

            var generator = new OrderGenerator(userIds);
            var orders = generator.Generate(count);

            await using var writer = await conn.BeginBinaryImportAsync(
                "COPY orders (user_id, order_number, order_status, subtotal, discount_amount, tax_amount, shipping_fee, total_amount, coupon_id, created_at, updated_at) FROM STDIN BINARY");

            var progress = 0;
            foreach (var order in orders)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(order.UserId, NpgsqlDbType.Bigint);
                await writer.WriteAsync(order.OrderNumber, NpgsqlDbType.Varchar);
                await writer.WriteAsync(order.OrderStatus, NpgsqlDbType.Varchar);
                await writer.WriteAsync(order.Subtotal, NpgsqlDbType.Numeric);
                await writer.WriteAsync(order.DiscountAmount, NpgsqlDbType.Numeric);
                await writer.WriteAsync(order.TaxAmount, NpgsqlDbType.Numeric);
                await writer.WriteAsync(order.ShippingFee, NpgsqlDbType.Numeric);
                await writer.WriteAsync(order.TotalAmount, NpgsqlDbType.Numeric);
                await writer.WriteAsync(order.CouponId, NpgsqlDbType.Bigint);
                await writer.WriteAsync(order.CreatedAt, NpgsqlDbType.Timestamp);
                await writer.WriteAsync(order.UpdatedAt, NpgsqlDbType.Timestamp);

                if (++progress % 100000 == 0)
                {
                    ctx.Status($"[yellow]주문 {progress:N0}/{count:N0} 생성 중... ({(progress * 100.0 / count):F1}%)[/]");
                }
            }

            await writer.CompleteAsync();
        });

    AnsiConsole.MarkupLine($"[green]✓[/] 주문 {count:N0}건 생성 완료");
}

static class Extensions
{
    public static T Also<T>(this T obj, Action<T> action)
    {
        action(obj);
        return obj;
    }
}
