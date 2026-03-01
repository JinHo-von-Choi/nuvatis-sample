using Microsoft.EntityFrameworkCore;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.EfCore.DbContexts;

namespace NuVatis.Benchmark.EfCore.Repositories;

public class EfCoreUserRepository : IUserRepository
{
    private readonly BenchmarkDbContext _context;

    public EfCoreUserRepository(BenchmarkDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(long id)
        => await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

    public async Task<IEnumerable<User>> GetByEmailDomainAsync(string domain)
        => await _context.Users.AsNoTracking()
            .Where(u => u.Email.Contains($"@{domain}"))
            .Take(100)
            .ToListAsync();

    public async Task<IEnumerable<User>> GetPagedAsync(int offset, int limit)
        => await _context.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

    public async Task<long> InsertAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    public async Task<int> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return await _context.SaveChangesAsync();
    }

    public async Task<User?> GetWithAddressesAsync(long id)
        => await _context.Users.AsNoTracking()
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<Dictionary<string, int>> GetUserCountByCountryAsync()
    {
        var result = await _context.Users.AsNoTracking()
            .SelectMany(u => u.Addresses!)
            .GroupBy(a => a.Country)
            .Select(g => new { Country = g.Key, Count = g.Select(a => a.UserId).Distinct().Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return result.ToDictionary(x => x.Country, x => x.Count);
    }

    public async Task<IEnumerable<User>> SearchAsync(string? userName, string? email, bool? isActive)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(userName))
            query = query.Where(u => u.UserName.Contains(userName));

        if (!string.IsNullOrEmpty(email))
            query = query.Where(u => u.Email.Contains(email));

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        return await query.OrderByDescending(u => u.CreatedAt).Take(1000).ToListAsync();
    }

    public async Task<int> BulkInsertAsync(IEnumerable<User> users)
    {
        await _context.Users.AddRangeAsync(users);
        return await _context.SaveChangesAsync();
    }

    public async Task<int> InsertUserWithAddressAsync(User user, Address address)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            address.UserId = user.Id;
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return 1;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
