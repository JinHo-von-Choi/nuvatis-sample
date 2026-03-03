using NuVatis.Attributes;
using NuVatis.Benchmark.Core.Models;

namespace BenchmarkNuVatis.Mappers;

[NuVatisMapper]
public interface IReviewMapper
{
    Task<Review?> GetByIdAsync(long id);
    Task<IEnumerable<Review>> GetByProductIdAsync(long productId);
    Task<IEnumerable<Review>> GetWithDetailsAsync(long productId, int limit);
    Task<Dictionary<long, double>> GetAverageRatingByProductAsync();
    Task<IEnumerable<dynamic>> GetTopReviewsByProductAsync(int topN);
    Task<long> InsertAsync(Review review);
    Task<int> BulkInsertAsync(IEnumerable<Review> reviews);
}
