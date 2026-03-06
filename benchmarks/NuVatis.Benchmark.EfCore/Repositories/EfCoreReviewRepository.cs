using System.Data;
using Microsoft.EntityFrameworkCore;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.EfCore.DbContexts;

namespace NuVatis.Benchmark.EfCore.Repositories;

public class EfCoreReviewRepository : IReviewRepository
{
    private readonly BenchmarkDbContext _context;

    public EfCoreReviewRepository(BenchmarkDbContext context)
    {
        _context = context;
    }

    public async Task<Review?> GetByIdAsync(long id)
    {
        return await _context.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Review>> GetByProductIdAsync(long productId)
    {
        return await _context.Reviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetWithDetailsAsync(long productId, int limit)
    {
        return await _context.Reviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<long, double>> GetAverageRatingByProductAsync()
    {
        var result = await _context.Reviews.AsNoTracking()
            .GroupBy(r => r.ProductId)
            .Select(g => new { ProductId = g.Key, AvgRating = g.Average(r => r.Rating) })
            .OrderByDescending(x => x.AvgRating)
            .ToDictionaryAsync(x => x.ProductId, x => x.AvgRating);

        return result;
    }

    public async Task<IEnumerable<dynamic>> GetTopReviewsByProductAsync(int topN)
    {
        // EF Core FromSqlRaw은 내부 shadow property(UserId1) 매핑 실패로 사용 불가.
        // 직접 ADO.NET으로 실행하여 EF Core 모델 매핑 우회.
        const string sql = @"
            WITH RankedReviews AS (
                SELECT r.id, r.user_id, r.product_id, r.rating, r.title, r.content,
                       r.is_verified, r.helpful_count, r.created_at, r.updated_at,
                       ROW_NUMBER() OVER (PARTITION BY r.product_id ORDER BY r.rating DESC, r.created_at DESC) AS rank
                FROM reviews r
            )
            SELECT id, user_id, product_id, rating, title, content,
                   is_verified, helpful_count, created_at, updated_at
            FROM RankedReviews
            WHERE rank <= @topN
            ORDER BY product_id, rank";

        var conn = _context.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var p = cmd.CreateParameter();
            p.ParameterName = "topN";
            p.Value         = topN;
            cmd.Parameters.Add(p);

            var results = new List<Review>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new Review
                {
                    Id           = reader.GetInt64(reader.GetOrdinal("id")),
                    UserId       = reader.GetInt64(reader.GetOrdinal("user_id")),
                    ProductId    = reader.GetInt64(reader.GetOrdinal("product_id")),
                    Rating       = reader.GetInt32(reader.GetOrdinal("rating")),
                    Title        = reader.IsDBNull(reader.GetOrdinal("title"))   ? null : reader.GetString(reader.GetOrdinal("title")),
                    Content      = reader.IsDBNull(reader.GetOrdinal("content")) ? null : reader.GetString(reader.GetOrdinal("content")),
                    IsVerified   = reader.GetBoolean(reader.GetOrdinal("is_verified")),
                    HelpfulCount = reader.GetInt32(reader.GetOrdinal("helpful_count")),
                    CreatedAt    = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt    = reader.GetDateTime(reader.GetOrdinal("updated_at")),
                });
            }
            return results.Cast<dynamic>();
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
    }

    public async Task<long> InsertAsync(Review review)
    {
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();
        return review.Id;
    }

    public async Task<int> BulkInsertAsync(IEnumerable<Review> reviews)
    {
        _context.Reviews.AddRange(reviews);
        return await _context.SaveChangesAsync();
    }
}
