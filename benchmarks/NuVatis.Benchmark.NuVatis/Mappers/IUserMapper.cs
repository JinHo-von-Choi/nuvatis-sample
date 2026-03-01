using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.NuVatis.Mappers;

/**
 * NuVatis 사용자 매퍼 인터페이스
 * XML 매퍼와 1:1 매핑
 */
public interface IUserMapper
{
    Task<User?> GetByIdAsync(long id);
    Task<IEnumerable<User>> GetByEmailDomainAsync(string domain);
    Task<IEnumerable<User>> GetPagedAsync(int offset, int limit);
    Task<long> InsertAsync(User user);
    Task<int> UpdateAsync(User user);
    Task<User?> GetWithAddressesAsync(long id);
    Task<Dictionary<string, int>> GetUserCountByCountryAsync();
    Task<IEnumerable<User>> SearchAsync(string? userName, string? email, bool? isActive);
    Task<int> BulkInsertAsync(IEnumerable<User> users);
}
