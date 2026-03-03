using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using BenchmarkNuVatis.Mappers;

namespace BenchmarkNuVatis.Repositories;

public class NuVatisReviewRepository : IReviewRepository
{
    private readonly IReviewMapper _mapper;

    public NuVatisReviewRepository(IReviewMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<Review?> GetByIdAsync(long id)
        => await _mapper.GetByIdAsync(id);

    public async Task<IEnumerable<Review>> GetByProductIdAsync(long productId)
        => await _mapper.GetByProductIdAsync(productId);

    public async Task<IEnumerable<Review>> GetWithDetailsAsync(long productId, int limit)
        => await _mapper.GetWithDetailsAsync(productId, limit);

    public async Task<Dictionary<long, double>> GetAverageRatingByProductAsync()
        => await _mapper.GetAverageRatingByProductAsync();

    public async Task<IEnumerable<dynamic>> GetTopReviewsByProductAsync(int topN)
        => await _mapper.GetTopReviewsByProductAsync(topN);

    public async Task<long> InsertAsync(Review review)
        => await _mapper.InsertAsync(review);

    public async Task<int> BulkInsertAsync(IEnumerable<Review> reviews)
        => await _mapper.BulkInsertAsync(reviews);
}
