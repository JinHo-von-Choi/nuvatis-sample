using Dapper;
using Npgsql;
using NuVatis.Benchmark.Core.Interfaces;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.Dapper.Repositories;

/**
 * Dapper 사용자 Repository 구현
 */
public class DapperUserRepository : IUserRepository
{
    private readonly string _connectionString;

    public DapperUserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, user_name AS UserName, email, full_name AS FullName, password_hash AS PasswordHash,
                   date_of_birth AS DateOfBirth, phone_number AS PhoneNumber, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE id = @Id";

        return await conn.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<IEnumerable<User>> GetByEmailDomainAsync(string domain)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, user_name AS UserName, email, full_name AS FullName, password_hash AS PasswordHash,
                   date_of_birth AS DateOfBirth, phone_number AS PhoneNumber, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            WHERE email LIKE '%@' || @Domain
            LIMIT 100";

        return await conn.QueryAsync<User>(sql, new { Domain = domain });
    }

    public async Task<IEnumerable<User>> GetPagedAsync(int offset, int limit)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT id, user_name AS UserName, email, full_name AS FullName, password_hash AS PasswordHash,
                   date_of_birth AS DateOfBirth, phone_number AS PhoneNumber, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset";

        return await conn.QueryAsync<User>(sql, new { Offset = offset, Limit = limit });
    }

    public async Task<long> InsertAsync(User user)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO users (user_name, email, full_name, password_hash, date_of_birth, phone_number, is_active, created_at, updated_at)
            VALUES (@UserName, @Email, @FullName, @PasswordHash, @DateOfBirth, @PhoneNumber, @IsActive, @CreatedAt, @UpdatedAt)
            RETURNING id";

        return await conn.ExecuteScalarAsync<long>(sql, user);
    }

    public async Task<int> UpdateAsync(User user)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            UPDATE users
            SET user_name = @UserName,
                email = @Email,
                full_name = @FullName,
                phone_number = @PhoneNumber,
                is_active = @IsActive,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id";

        return await conn.ExecuteAsync(sql, user);
    }

    public async Task<User?> GetWithAddressesAsync(long id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT
                u.id, u.user_name AS UserName, u.email, u.full_name AS FullName, u.password_hash AS PasswordHash,
                u.date_of_birth AS DateOfBirth, u.phone_number AS PhoneNumber, u.is_active AS IsActive,
                u.created_at AS CreatedAt, u.updated_at AS UpdatedAt,
                a.id, a.user_id AS UserId, a.address_type AS AddressType, a.street_address AS StreetAddress,
                a.city, a.state, a.postal_code AS PostalCode, a.country, a.is_default AS IsDefault,
                a.created_at AS CreatedAt, a.updated_at AS UpdatedAt
            FROM users u
            LEFT JOIN addresses a ON u.id = a.user_id
            WHERE u.id = @Id";

        var userDict = new Dictionary<long, User>();
        await conn.QueryAsync<User, Address, User>(sql,
            (user, address) =>
            {
                if (!userDict.TryGetValue(user.Id, out var existingUser))
                {
                    existingUser = user;
                    existingUser.Addresses = new List<Address>();
                    userDict.Add(user.Id, existingUser);
                }

                if (address != null)
                {
                    ((List<Address>)existingUser.Addresses!).Add(address);
                }

                return existingUser;
            },
            new { Id = id },
            splitOn: "id"
        );

        return userDict.Values.FirstOrDefault();
    }

    public async Task<Dictionary<string, int>> GetUserCountByCountryAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT a.country, COUNT(DISTINCT u.id) AS count
            FROM users u
            INNER JOIN addresses a ON u.id = a.user_id
            GROUP BY a.country
            ORDER BY count DESC";

        var result = await conn.QueryAsync<(string country, int count)>(sql);
        return result.ToDictionary(x => x.country, x => x.count);
    }

    public async Task<IEnumerable<User>> SearchAsync(string? userName, string? email, bool? isActive)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(userName))
        {
            conditions.Add("user_name LIKE '%' || @UserName || '%'");
            parameters.Add("UserName", userName);
        }

        if (!string.IsNullOrEmpty(email))
        {
            conditions.Add("email LIKE '%' || @Email || '%'");
            parameters.Add("Email", email);
        }

        if (isActive.HasValue)
        {
            conditions.Add("is_active = @IsActive");
            parameters.Add("IsActive", isActive.Value);
        }

        var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $@"
            SELECT id, user_name AS UserName, email, full_name AS FullName, password_hash AS PasswordHash,
                   date_of_birth AS DateOfBirth, phone_number AS PhoneNumber, is_active AS IsActive,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM users
            {whereClause}
            ORDER BY created_at DESC
            LIMIT 1000";

        return await conn.QueryAsync<User>(sql, parameters);
    }

    public async Task<int> BulkInsertAsync(IEnumerable<User> users)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var writer = conn.BeginBinaryImport(
            "COPY users (user_name, email, full_name, password_hash, date_of_birth, phone_number, is_active, created_at, updated_at) FROM STDIN BINARY");

        foreach (var user in users)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(user.UserName);
            await writer.WriteAsync(user.Email);
            await writer.WriteAsync(user.FullName);
            await writer.WriteAsync(user.PasswordHash);
            await writer.WriteAsync(user.DateOfBirth, NpgsqlTypes.NpgsqlDbType.Date);
            await writer.WriteAsync(user.PhoneNumber);
            await writer.WriteAsync(user.IsActive);
            await writer.WriteAsync(user.CreatedAt, NpgsqlTypes.NpgsqlDbType.Timestamp);
            await writer.WriteAsync(user.UpdatedAt, NpgsqlTypes.NpgsqlDbType.Timestamp);
        }

        var rows = await writer.CompleteAsync();
        return (int)rows;
    }

    public async Task<int> InsertUserWithAddressAsync(User user, Address address)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            const string userSql = @"
                INSERT INTO users (user_name, email, full_name, password_hash, date_of_birth, phone_number, is_active, created_at, updated_at)
                VALUES (@UserName, @Email, @FullName, @PasswordHash, @DateOfBirth, @PhoneNumber, @IsActive, @CreatedAt, @UpdatedAt)
                RETURNING id";

            var userId = await conn.ExecuteScalarAsync<long>(userSql, user, transaction);

            const string addressSql = @"
                INSERT INTO addresses (user_id, address_type, street_address, city, state, postal_code, country, is_default, created_at, updated_at)
                VALUES (@UserId, @AddressType, @StreetAddress, @City, @State, @PostalCode, @Country, @IsDefault, @CreatedAt, @UpdatedAt)";

            address.UserId = userId;
            await conn.ExecuteAsync(addressSql, address, transaction);

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
