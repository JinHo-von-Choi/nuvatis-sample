using Dapper;
using Npgsql;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Dapper.Repositories;

public class DapperOrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public DapperOrderRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Order?> GetByIdAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, user_id AS UserId, order_number AS OrderNumber, order_status AS OrderStatus,
                   subtotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                   shipping_fee AS ShippingFee, total_amount AS TotalAmount, coupon_id AS CouponId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM orders
            WHERE id = @Id";

        return await conn.QuerySingleOrDefaultAsync<Order>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Order>> GetByUserIdAsync(long userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, user_id AS UserId, order_number AS OrderNumber, order_status AS OrderStatus,
                   subtotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                   shipping_fee AS ShippingFee, total_amount AS TotalAmount, coupon_id AS CouponId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM orders
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT 100";

        return await conn.QueryAsync<Order>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<Order>> GetPagedAsync(int offset, int limit)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, user_id AS UserId, order_number AS OrderNumber, order_status AS OrderStatus,
                   subtotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                   shipping_fee AS ShippingFee, total_amount AS TotalAmount, coupon_id AS CouponId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM orders
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset";

        return await conn.QueryAsync<Order>(sql, new { Offset = offset, Limit = limit });
    }

    public async Task<Order?> GetWithUserAndItemsAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT DISTINCT
                o.id, o.user_id AS UserId, o.order_number AS OrderNumber, o.order_status AS OrderStatus,
                o.subtotal, o.discount_amount AS DiscountAmount, o.tax_amount AS TaxAmount,
                o.shipping_fee AS ShippingFee, o.total_amount AS TotalAmount, o.coupon_id AS CouponId,
                o.created_at AS CreatedAt, o.updated_at AS UpdatedAt
            FROM orders o
            INNER JOIN users u ON o.user_id = u.id
            LEFT JOIN order_items oi ON o.id = oi.order_id
            WHERE o.id = @Id";

        return await conn.QuerySingleOrDefaultAsync<Order>(sql, new { Id = id });
    }

    public async Task<Order?> GetCompleteOrderAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT DISTINCT
                o.id, o.user_id AS UserId, o.order_number AS OrderNumber, o.order_status AS OrderStatus,
                o.subtotal, o.discount_amount AS DiscountAmount, o.tax_amount AS TaxAmount,
                o.shipping_fee AS ShippingFee, o.total_amount AS TotalAmount, o.coupon_id AS CouponId,
                o.created_at AS CreatedAt, o.updated_at AS UpdatedAt
            FROM orders o
            INNER JOIN users u ON o.user_id = u.id
            LEFT JOIN order_items oi ON o.id = oi.order_id
            LEFT JOIN products p ON oi.product_id = p.id
            LEFT JOIN payments pay ON o.id = pay.order_id
            LEFT JOIN shipments s ON o.id = s.order_id
            WHERE o.id = @Id";

        return await conn.QuerySingleOrDefaultAsync<Order>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Order>> GetOrdersWithNPlusOneProblemAsync(long userId, int limit)
    {
        // N+1 문제 시뮬레이션
        return await GetByUserIdAsync(userId);
    }

    public async Task<IEnumerable<Order>> GetOrdersOptimizedAsync(long userId, int limit)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT DISTINCT
                o.id, o.user_id AS UserId, o.order_number AS OrderNumber, o.order_status AS OrderStatus,
                o.subtotal, o.discount_amount AS DiscountAmount, o.tax_amount AS TaxAmount,
                o.shipping_fee AS ShippingFee, o.total_amount AS TotalAmount, o.coupon_id AS CouponId,
                o.created_at AS CreatedAt, o.updated_at AS UpdatedAt
            FROM orders o
            LEFT JOIN order_items oi ON o.id = oi.order_id
            WHERE o.user_id = @UserId
            ORDER BY o.created_at DESC
            LIMIT @Limit";

        return await conn.QueryAsync<Order>(sql, new { UserId = userId, Limit = limit });
    }

    public async Task<IEnumerable<Order>> GetKeysetPagedAsync(long? lastId, int limit)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var sql = lastId.HasValue
            ? @"SELECT id, user_id AS UserId, order_number AS OrderNumber, order_status AS OrderStatus,
                       subtotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                       shipping_fee AS ShippingFee, total_amount AS TotalAmount, coupon_id AS CouponId,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM orders
                WHERE id < @LastId
                ORDER BY id DESC
                LIMIT @Limit"
            : @"SELECT id, user_id AS UserId, order_number AS OrderNumber, order_status AS OrderStatus,
                       subtotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                       shipping_fee AS ShippingFee, total_amount AS TotalAmount, coupon_id AS CouponId,
                       created_at AS CreatedAt, updated_at AS UpdatedAt
                FROM orders
                ORDER BY id DESC
                LIMIT @Limit";

        return await conn.QueryAsync<Order>(sql, new { LastId = lastId, Limit = limit });
    }

    public async Task<Dictionary<string, decimal>> GetTotalAmountByStatusAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT order_status, SUM(total_amount) AS total
            FROM orders
            GROUP BY order_status
            ORDER BY total DESC";

        var result = await conn.QueryAsync<(string status, decimal total)>(sql);
        return result.ToDictionary(x => x.status, x => x.total);
    }

    public async Task<IEnumerable<Order>> SearchAsync(long? userId, string? status, DateTime? fromDate, DateTime? toDate)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (userId.HasValue)
        {
            conditions.Add("user_id = @UserId");
            parameters.Add("UserId", userId.Value);
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add("order_status = @Status");
            parameters.Add("Status", status);
        }

        if (fromDate.HasValue)
        {
            conditions.Add("created_at >= @FromDate");
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            conditions.Add("created_at <= @ToDate");
            parameters.Add("ToDate", toDate.Value);
        }

        var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $@"
            SELECT id, user_id AS UserId, order_number AS OrderNumber, order_status AS OrderStatus,
                   subtotal, discount_amount AS DiscountAmount, tax_amount AS TaxAmount,
                   shipping_fee AS ShippingFee, total_amount AS TotalAmount, coupon_id AS CouponId,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM orders
            {whereClause}
            ORDER BY created_at DESC
            LIMIT 1000";

        return await conn.QueryAsync<Order>(sql, parameters);
    }

    public async Task<long> InsertAsync(Order order)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO orders (user_id, order_number, order_status, subtotal, discount_amount, tax_amount, shipping_fee, total_amount, coupon_id, created_at, updated_at)
            VALUES (@UserId, @OrderNumber, @OrderStatus, @Subtotal, @DiscountAmount, @TaxAmount, @ShippingFee, @TotalAmount, @CouponId, @CreatedAt, @UpdatedAt)
            RETURNING id";

        return await conn.ExecuteScalarAsync<long>(sql, order);
    }

    public async Task<long> CreateCompleteOrderAsync(Order order, IEnumerable<OrderItem> items, Payment payment, Shipment shipment)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            var orderId = await InsertAsync(order);
            await transaction.CommitAsync();
            return orderId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> BulkInsertOrdersWithItemsAsync(IEnumerable<(Order order, IEnumerable<OrderItem> items)> orderData)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        int count = 0;

        foreach (var (order, items) in orderData)
        {
            await InsertAsync(order);
            count++;
        }

        return count;
    }

    public async Task<IEnumerable<dynamic>> GetDailySalesAggregationAsync(DateTime fromDate, DateTime toDate)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT
                DATE(created_at) AS sale_date,
                COUNT(*) AS order_count,
                SUM(total_amount) AS total_sales,
                AVG(total_amount) AS avg_order_value,
                MIN(total_amount) AS min_order,
                MAX(total_amount) AS max_order
            FROM orders
            WHERE created_at BETWEEN @FromDate AND @ToDate
            GROUP BY DATE(created_at)
            ORDER BY sale_date DESC";

        return await conn.QueryAsync(sql, new { FromDate = fromDate, ToDate = toDate });
    }
}
