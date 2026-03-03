using NuVatis.Attributes;
using NuVatis.Benchmark.Core.Models;

namespace BenchmarkNuVatis.Mappers;

[NuVatisMapper]
public interface IProductMapper
{
    Task<Product?> GetByIdAsync(long id);
    Task<IEnumerable<Product>> GetByCategoryIdAsync(long categoryId);
    Task<Product?> GetWithDetailsAsync(long id);
    Task<Dictionary<long, decimal>> GetTotalSalesByCategoryAsync();
    Task<IEnumerable<dynamic>> GetProductsWithPriceRankAsync();
    Task<IEnumerable<Product>> GetAllActiveProductsAsync();
    Task<int> UpdateStockAsync(long productId, int newStock);
}
