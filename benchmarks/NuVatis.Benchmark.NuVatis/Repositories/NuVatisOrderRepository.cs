using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.NuVatis.Mappers;

namespace NuVatis.Benchmark.NuVatis.Repositories;

public class NuVatisOrderRepository : IOrderRepository
{
    private readonly IOrderMapper _mapper;

    public NuVatisOrderRepository(IOrderMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<Order?> GetByIdAsync(long id)
        => await _mapper.GetByIdAsync(id);

    public async Task<IEnumerable<Order>> GetByUserIdAsync(long userId)
        => await _mapper.GetByUserIdAsync(userId);

    public async Task<IEnumerable<Order>> GetPagedAsync(int offset, int limit)
        => await _mapper.GetPagedAsync(offset, limit);

    public async Task<Order?> GetWithUserAndItemsAsync(long id)
        => await _mapper.GetWithUserAndItemsAsync(id);

    public async Task<Order?> GetCompleteOrderAsync(long id)
        => await _mapper.GetCompleteOrderAsync(id);

    public async Task<IEnumerable<Order>> GetOrdersWithNPlusOneProblemAsync(long userId, int limit)
        => await _mapper.GetOrdersWithNPlusOneProblemAsync(userId, limit);

    public async Task<IEnumerable<Order>> GetOrdersOptimizedAsync(long userId, int limit)
        => await _mapper.GetOrdersOptimizedAsync(userId, limit);

    public async Task<IEnumerable<Order>> GetKeysetPagedAsync(long? lastId, int limit)
        => await _mapper.GetKeysetPagedAsync(lastId, limit);

    public async Task<Dictionary<string, decimal>> GetTotalAmountByStatusAsync()
        => await _mapper.GetTotalAmountByStatusAsync();

    public async Task<IEnumerable<Order>> SearchAsync(long? userId, string? status, DateTime? fromDate, DateTime? toDate)
        => await _mapper.SearchAsync(userId, status, fromDate, toDate);

    public async Task<long> InsertAsync(Order order)
        => await _mapper.InsertAsync(order);

    public async Task<long> CreateCompleteOrderAsync(Order order, IEnumerable<OrderItem> items, Payment payment, Shipment shipment)
    {
        // 트랜잭션 처리 필요 - 간소화
        var orderId = await _mapper.InsertAsync(order);
        return orderId;
    }

    public async Task<int> BulkInsertOrdersWithItemsAsync(IEnumerable<(Order order, IEnumerable<OrderItem> items)> orderData)
    {
        // 벌크 삽입 로직 - 간소화
        int count = 0;
        foreach (var (order, items) in orderData)
        {
            await _mapper.InsertAsync(order);
            count++;
        }
        return count;
    }

    public async Task<IEnumerable<dynamic>> GetDailySalesAggregationAsync(DateTime fromDate, DateTime toDate)
        => await _mapper.GetDailySalesAggregationAsync(fromDate, toDate);
}
