using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using BenchmarkNuVatis.Mappers;

namespace BenchmarkNuVatis.Repositories;

public class NuVatisProductRepository : IProductRepository
{
    private readonly IProductMapper _mapper;

    public NuVatisProductRepository(IProductMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<Product?> GetByIdAsync(long id)
        => await _mapper.GetByIdAsync(id);

    public async Task<IEnumerable<Product>> GetByCategoryIdAsync(long categoryId)
        => await _mapper.GetByCategoryIdAsync(categoryId);

    public async Task<Product?> GetWithDetailsAsync(long id)
        => await _mapper.GetWithDetailsAsync(id);

    public async Task<Dictionary<long, decimal>> GetTotalSalesByCategoryAsync()
        => await _mapper.GetTotalSalesByCategoryAsync();

    public async Task<IEnumerable<dynamic>> GetProductsWithPriceRankAsync()
        => await _mapper.GetProductsWithPriceRankAsync();

    public async Task<IEnumerable<Product>> GetAllActiveProductsAsync()
        => await _mapper.GetAllActiveProductsAsync();

    public async Task<int> UpdateStockAsync(long productId, int newStock)
        => await _mapper.UpdateStockAsync(productId, newStock);
}
