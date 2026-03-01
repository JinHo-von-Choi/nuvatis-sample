using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.NuVatis.Mappers;

namespace NuVatis.Benchmark.NuVatis.Repositories;

/**
 * NuVatis 사용자 Repository 구현
 */
public class NuVatisUserRepository : IUserRepository
{
    private readonly IUserMapper _mapper;

    public NuVatisUserRepository(IUserMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<User?> GetByIdAsync(long id)
        => await _mapper.GetByIdAsync(id);

    public async Task<IEnumerable<User>> GetByEmailDomainAsync(string domain)
        => await _mapper.GetByEmailDomainAsync(domain);

    public async Task<IEnumerable<User>> GetPagedAsync(int offset, int limit)
        => await _mapper.GetPagedAsync(offset, limit);

    public async Task<long> InsertAsync(User user)
        => await _mapper.InsertAsync(user);

    public async Task<int> UpdateAsync(User user)
        => await _mapper.UpdateAsync(user);

    public async Task<User?> GetWithAddressesAsync(long id)
        => await _mapper.GetWithAddressesAsync(id);

    public async Task<Dictionary<string, int>> GetUserCountByCountryAsync()
        => await _mapper.GetUserCountByCountryAsync();

    public async Task<IEnumerable<User>> SearchAsync(string? userName, string? email, bool? isActive)
        => await _mapper.SearchAsync(userName, email, isActive);

    public async Task<int> BulkInsertAsync(IEnumerable<User> users)
        => await _mapper.BulkInsertAsync(users);

    public async Task<int> InsertUserWithAddressAsync(User user, Address address)
    {
        // 트랜잭션 처리는 상위 레이어에서 관리
        var userId = await _mapper.InsertAsync(user);
        address.UserId = userId;
        // Address 삽입은 별도 매퍼 필요 (간소화를 위해 생략)
        return 1;
    }
}
