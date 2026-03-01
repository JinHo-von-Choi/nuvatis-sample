using Microsoft.EntityFrameworkCore;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.EfCore.DbContexts;

namespace NuVatis.Benchmark.EfCore.Repositories;

public class EfCoreOrderRepository : IOrderRepository
{
    private readonly BenchmarkDbContext _context;

    public EfCoreOrderRepository(BenchmarkDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(long id)
        => await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IEnumerable<Order>> GetByUserIdAsync(long userId)
        => await _context.Orders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync();

    public async Task<IEnumerable<Order>> GetPagedAsync(int offset, int limit)
        => await _context.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

    public async Task<Order?> GetWithUserAndItemsAsync(long id)
        => await _context.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<Order?> GetCompleteOrderAsync(long id)
        => await _context.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.OrderItems!)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Payment)
            .Include(o => o.Shipment)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IEnumerable<Order>> GetOrdersWithNPlusOneProblemAsync(long userId, int limit)
    {
        // N+1 문제 시뮬레이션 (Include 없음)
        return await _context.Orders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetOrdersOptimizedAsync(long userId, int limit)
    {
        // Eager Loading으로 최적화
        return await _context.Orders.AsNoTracking()
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetKeysetPagedAsync(long? lastId, int limit)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (lastId.HasValue)
            query = query.Where(o => o.Id < lastId.Value);

        return await query.OrderByDescending(o => o.Id).Take(limit).ToListAsync();
    }

    public async Task<Dictionary<string, decimal>> GetTotalAmountByStatusAsync()
    {
        var result = await _context.Orders.AsNoTracking()
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { Status = g.Key, Total = g.Sum(o => o.TotalAmount) })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        return result.ToDictionary(x => x.Status, x => x.Total);
    }

    public async Task<IEnumerable<Order>> SearchAsync(long? userId, string? status, DateTime? fromDate, DateTime? toDate)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.OrderStatus == status);

        if (fromDate.HasValue)
            query = query.Where(o => o.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.CreatedAt <= toDate.Value);

        return await query.OrderByDescending(o => o.CreatedAt).Take(1000).ToListAsync();
    }

    public async Task<long> InsertAsync(Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order.Id;
    }

    public async Task<long> CreateCompleteOrderAsync(Order order, IEnumerable<OrderItem> items, Payment payment, Shipment shipment)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return order.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> BulkInsertOrdersWithItemsAsync(IEnumerable<(Order order, IEnumerable<OrderItem> items)> orderData)
    {
        int count = 0;
        foreach (var (order, items) in orderData)
        {
            _context.Orders.Add(order);
            count++;
        }

        await _context.SaveChangesAsync();
        return count;
    }

    public async Task<IEnumerable<dynamic>> GetDailySalesAggregationAsync(DateTime fromDate, DateTime toDate)
    {
        var result = await _context.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                sale_date = g.Key,
                order_count = g.Count(),
                total_sales = g.Sum(o => o.TotalAmount),
                avg_order_value = g.Average(o => o.TotalAmount),
                min_order = g.Min(o => o.TotalAmount),
                max_order = g.Max(o => o.TotalAmount)
            })
            .OrderByDescending(x => x.sale_date)
            .ToListAsync();

        return result.Cast<dynamic>();
    }
}
