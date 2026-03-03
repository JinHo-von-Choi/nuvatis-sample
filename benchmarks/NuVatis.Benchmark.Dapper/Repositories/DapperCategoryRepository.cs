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
            "SELECT id, parent_id, name AS CategoryName, description, created_at AS CreatedAt FROM categories WHERE id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<Category>> GetCategoryTreeAsync(long rootId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            WITH RECURSIVE category_tree AS (
                SELECT id, parent_id, name, description, created_at, 1 AS level
                FROM categories WHERE id = @RootId
                UNION ALL
                SELECT c.id, c.parent_id, c.name, c.description, c.created_at, ct.level + 1
                FROM categories c
                INNER JOIN category_tree ct ON c.parent_id = ct.id
            )
            SELECT id, parent_id, name AS CategoryName, description, created_at AS CreatedAt
            FROM category_tree ORDER BY level, id";

        return await conn.QueryAsync<Category>(sql, new { RootId = rootId });
    }

    public async Task<IEnumerable<Category>> GetAllDescendantsAsync(long parentId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            WITH RECURSIVE descendants AS (
                SELECT id, parent_id, name, description, created_at
                FROM categories WHERE parent_id = @ParentId
                UNION ALL
                SELECT c.id, c.parent_id, c.name, c.description, c.created_at
                FROM categories c
                INNER JOIN descendants d ON c.parent_id = d.id
            )
            SELECT id, parent_id, name AS CategoryName, description, created_at AS CreatedAt
            FROM descendants ORDER BY id";

        return await conn.QueryAsync<Category>(sql, new { ParentId = parentId });
    }

    public async Task<long> InsertAsync(Category category)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO categories (parent_id, name, description, created_at)
            VALUES (@ParentId, @CategoryName, @Description, @CreatedAt)
            RETURNING id";

        return await conn.ExecuteScalarAsync<long>(sql, category);
    }
}
