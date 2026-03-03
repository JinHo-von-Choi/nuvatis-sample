using Dapper;
using Npgsql;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Dapper.Repositories;

public class DapperReviewRepository : IReviewRepository
{
    private readonly string _connectionString;

    public DapperReviewRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Review?> GetByIdAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Review>(
            "SELECT id, user_id AS UserId, product_id AS ProductId, rating, comment AS Content, created_at AS CreatedAt FROM reviews WHERE id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<Review>> GetByProductIdAsync(long productId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<Review>(
            "SELECT id, user_id AS UserId, product_id AS ProductId, rating, comment AS Content, created_at AS CreatedAt FROM reviews WHERE product_id = @ProductId ORDER BY created_at DESC LIMIT 100",
            new { ProductId = productId });
    }

    public async Task<IEnumerable<Review>> GetWithDetailsAsync(long productId, int limit)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT r.id, r.user_id AS UserId, r.product_id AS ProductId, r.rating, r.comment AS Content, r.created_at AS CreatedAt
            FROM reviews r
            INNER JOIN users u ON r.user_id = u.id
            INNER JOIN products p ON r.product_id = p.id
            WHERE r.product_id = @ProductId
            ORDER BY r.created_at DESC
            LIMIT @Limit";

        return await conn.QueryAsync<Review>(sql, new { ProductId = productId, Limit = limit });
    }

    public async Task<Dictionary<long, double>> GetAverageRatingByProductAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT product_id, AVG(rating::decimal) AS avg_rating
            FROM reviews
            GROUP BY product_id
            HAVING COUNT(*) >= 5
            ORDER BY avg_rating DESC";

        var result = await conn.QueryAsync<(long productId, double avgRating)>(sql);
        return result.ToDictionary(x => x.productId, x => x.avgRating);
    }

    public async Task<IEnumerable<dynamic>> GetTopReviewsByProductAsync(int topN)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            WITH ranked_reviews AS (
                SELECT
                    id, product_id, user_id, rating,
                    ROW_NUMBER() OVER (PARTITION BY product_id ORDER BY rating DESC, created_at DESC) AS rank
                FROM reviews
            )
            SELECT product_id, user_id, rating, rank
            FROM ranked_reviews
            WHERE rank <= @TopN
            ORDER BY product_id, rank";

        return await conn.QueryAsync(sql, new { TopN = topN });
    }

    public async Task<long> InsertAsync(Review review)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO reviews (user_id, product_id, rating, comment, created_at)
            VALUES (@UserId, @ProductId, @Rating, @Content, @CreatedAt)
            RETURNING id";

        return await conn.ExecuteScalarAsync<long>(sql, review);
    }

    public async Task<int> BulkInsertAsync(IEnumerable<Review> reviews)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var writer = conn.BeginBinaryImport(
            "COPY reviews (user_id, product_id, rating, comment, created_at) FROM STDIN BINARY");

        foreach (var review in reviews)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(review.UserId);
            await writer.WriteAsync(review.ProductId);
            await writer.WriteAsync(review.Rating);
            await writer.WriteAsync(review.Content); // DB: comment
            await writer.WriteAsync(review.CreatedAt, NpgsqlTypes.NpgsqlDbType.Timestamp);
        }

        var rows = await writer.CompleteAsync();
        return (int)rows;
    }
}
