using Dapper;
using Npgsql;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Dapper.Repositories;

public class DapperCategoryRepository : ICategoryRepository
{
    private readonly string _connectionString;

    public DapperCategoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Category?> GetByIdAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<Category>(
            "SELECT * FROM categories WHERE id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<Category>> GetCategoryTreeAsync(long rootId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            WITH RECURSIVE category_tree AS (
                SELECT *, 1 AS level FROM categories WHERE id = @RootId
                UNION ALL
                SELECT c.*, ct.level + 1
                FROM categories c
                INNER JOIN category_tree ct ON c.parent_id = ct.id
                WHERE c.is_active = true
            )
            SELECT * FROM category_tree ORDER BY level, display_order";

        return await conn.QueryAsync<Category>(sql, new { RootId = rootId });
    }

    public async Task<IEnumerable<Category>> GetAllDescendantsAsync(long parentId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            WITH RECURSIVE descendants AS (
                SELECT * FROM categories WHERE parent_id = @ParentId
                UNION ALL
                SELECT c.* FROM categories c
                INNER JOIN descendants d ON c.parent_id = d.id
            )
            SELECT * FROM descendants ORDER BY display_order";

        return await conn.QueryAsync<Category>(sql, new { ParentId = parentId });
    }

    public async Task<long> InsertAsync(Category category)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO categories (parent_id, category_name, description, display_order, is_active, created_at, updated_at)
            VALUES (@ParentId, @CategoryName, @Description, @DisplayOrder, @IsActive, @CreatedAt, @UpdatedAt)
            RETURNING id";

        return await conn.ExecuteScalarAsync<long>(sql, category);
    }
}
