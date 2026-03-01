using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;
using NuVatis.Benchmark.NuVatis.Mappers;

namespace NuVatis.Benchmark.NuVatis.Repositories;

public class NuVatisCategoryRepository : ICategoryRepository
{
    private readonly ICategoryMapper _mapper;

    public NuVatisCategoryRepository(ICategoryMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<Category?> GetByIdAsync(long id)
        => await _mapper.GetByIdAsync(id);

    public async Task<IEnumerable<Category>> GetCategoryTreeAsync(long rootId)
        => await _mapper.GetCategoryTreeAsync(rootId);

    public async Task<IEnumerable<Category>> GetAllDescendantsAsync(long parentId)
        => await _mapper.GetAllDescendantsAsync(parentId);

    public async Task<long> InsertAsync(Category category)
        => await _mapper.InsertAsync(category);
}
