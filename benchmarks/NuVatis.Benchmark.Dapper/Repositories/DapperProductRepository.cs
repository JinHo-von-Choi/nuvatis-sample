using Dapper;
using Npgsql;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Dapper.Repositories;

public class DapperProductRepository : IProductRepository
{
    private readonly string _connectionString;

    public DapperProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Product?> GetByIdAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Product>(
            "SELECT id, category_id AS CategoryId, name AS ProductName, description, price, stock AS StockQuantity, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM products WHERE id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<Product>> GetByCategoryIdAsync(long categoryId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<Product>(
            "SELECT id, category_id AS CategoryId, name AS ProductName, description, price, stock AS StockQuantity, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM products WHERE category_id = @CategoryId ORDER BY created_at DESC LIMIT 100",
            new { CategoryId = categoryId });
    }

    public async Task<Product?> GetWithDetailsAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT DISTINCT p.id, p.category_id AS CategoryId, p.name AS ProductName, p.description, p.price,
                   p.stock AS StockQuantity, p.is_active AS IsActive, p.created_at AS CreatedAt, p.updated_at AS UpdatedAt
            FROM products p
            INNER JOIN categories c ON p.category_id = c.id
            WHERE p.id = @Id";

        return await conn.QuerySingleOrDefaultAsync<Product>(sql, new { Id = id });
    }

    public async Task<Dictionary<long, decimal>> GetTotalSalesByCategoryAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT p.category_id, SUM(oi.subtotal) AS total
            FROM products p
            INNER JOIN order_items oi ON p.id = oi.product_id
            GROUP BY p.category_id
            ORDER BY total DESC";

        var result = await conn.QueryAsync<(long categoryId, decimal total)>(sql);
        return result.ToDictionary(x => x.categoryId, x => x.total);
    }

    public async Task<IEnumerable<dynamic>> GetProductsWithPriceRankAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT
                id, name AS product_name, price, category_id,
                ROW_NUMBER() OVER (PARTITION BY category_id ORDER BY price DESC) AS price_rank,
                RANK() OVER (ORDER BY price DESC) AS overall_rank,
                DENSE_RANK() OVER (PARTITION BY category_id ORDER BY price DESC) AS dense_rank
            FROM products
            WHERE is_active = true
            ORDER BY category_id, price_rank
            LIMIT 1000";

        return await conn.QueryAsync(sql);
    }

    public async Task<IEnumerable<Product>> GetAllActiveProductsAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<Product>(
            "SELECT id, category_id AS CategoryId, name AS ProductName, description, price, stock AS StockQuantity, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt FROM products WHERE is_active = true ORDER BY created_at DESC");
    }

    public async Task<int> UpdateStockAsync(long productId, int newStock)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteAsync(
            "UPDATE products SET stock = @NewStock, updated_at = CURRENT_TIMESTAMP WHERE id = @ProductId",
            new { ProductId = productId, NewStock = newStock });
    }
}
