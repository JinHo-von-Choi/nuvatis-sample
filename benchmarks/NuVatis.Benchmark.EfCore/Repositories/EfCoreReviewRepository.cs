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
        // EF Core는 Window Functions를 제한적으로 지원
        // Raw SQL 사용
        var sql = @"
            WITH RankedReviews AS (
                SELECT r.*,
                       ROW_NUMBER() OVER (PARTITION BY r.product_id ORDER BY r.rating DESC, r.created_at DESC) AS rank
                FROM reviews r
            )
            SELECT * FROM RankedReviews WHERE rank <= {0}
            ORDER BY product_id, rank";

        var results = await _context.Reviews
            .FromSqlRaw(sql, topN)
            .AsNoTracking()
            .ToListAsync();

        return results.Cast<dynamic>();
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
