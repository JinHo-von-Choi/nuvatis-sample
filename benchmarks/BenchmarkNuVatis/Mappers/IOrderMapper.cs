using NuVatis.Attributes;
using NuVatis.Benchmark.Core.Models;

namespace BenchmarkNuVatis.Mappers;

[NuVatisMapper]
public interface IOrderMapper
{
    Task<Order?> GetByIdAsync(long id);
    Task<IEnumerable<Order>> GetByUserIdAsync(long userId);
    Task<IEnumerable<Order>> GetPagedAsync(int offset, int limit);
    Task<Order?> GetWithUserAndItemsAsync(long id);
    Task<Order?> GetCompleteOrderAsync(long id);
    Task<IEnumerable<Order>> GetOrdersWithNPlusOneProblemAsync(long userId, int limit);
    Task<IEnumerable<Order>> GetOrdersOptimizedAsync(long userId, int limit);
    Task<IEnumerable<Order>> GetKeysetPagedAsync(long? lastId, int limit);
    Task<Dictionary<string, decimal>> GetTotalAmountByStatusAsync();
    Task<IEnumerable<Order>> SearchAsync(long? userId, string? status, DateTime? fromDate, DateTime? toDate);
    Task<long> InsertAsync(Order order);
    Task<IEnumerable<dynamic>> GetDailySalesAggregationAsync(DateTime fromDate, DateTime toDate);
}
