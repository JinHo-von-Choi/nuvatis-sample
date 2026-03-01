using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.NuVatis.Mappers;

public interface ICategoryMapper
{
    Task<Category?> GetByIdAsync(long id);
    Task<IEnumerable<Category>> GetCategoryTreeAsync(long rootId);
    Task<IEnumerable<Category>> GetAllDescendantsAsync(long parentId);
    Task<long> InsertAsync(Category category);
}
