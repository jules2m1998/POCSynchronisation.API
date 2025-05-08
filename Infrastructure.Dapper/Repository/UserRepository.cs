using Bogus;
using Dapper;

namespace Infrastructure.Dapper.Repository;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public Guid LastEventSynced { get; set; } = Guid.Empty;
}

public class UserRepository(IDbConnectionFactory dbConnectionFactory) : IBaserepositoryWithInitialisation<User, Guid>
{
    private readonly IDbConnectionFactory _dbConnectionFactory =
        dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));

    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();

        // Create the Users table if it doesn't exist
        var createTableSql = @"
                PRAGMA foreign_keys = ON;

                CREATE TABLE IF NOT EXISTS Users (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT NOT NULL UNIQUE,
                    LastEventSynced TEXT DEFAULT NULL
                );
            ";

        await connection.ExecuteAsync(createTableSql);

        // Check if any users exist
        var userCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users;");

        // Only create default user if none exist
        if (userCount == 0)
        {
            // Create a faker for generating a fake user
            var faker = new Faker<User>()
                .RuleFor(u => u.Id, f => Guid.CreateVersion7())
                .RuleFor(u => u.Name, f => f.Name.FullName())
                .RuleFor(u => u.Email, f => f.Internet.Email())
                .RuleFor(u => u.LastEventSynced, f => Guid.Empty);

            // Generate one fake user
            var defaultUser = faker.Generate();

            // Insert the user
            const string insertSql = @"
                    INSERT INTO Users (Id, Name, Email)
                    VALUES (@Id, @Name, @Email);";

            await connection.ExecuteAsync(insertSql, new
            {
                defaultUser.Id,
                defaultUser.Name,
                defaultUser.Email
            });
        }
    }

    private async Task<bool> CheckColumnExistsAsync(System.Data.IDbConnection connection, string tableName, string columnName)
    {
        var sql = $"PRAGMA table_info({tableName});";
        var tableInfo = await connection.QueryAsync(sql);

        return tableInfo.Any(row => string.Equals((string)row.name, columnName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        // Ensure we have a valid ID
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.CreateVersion7();
        }

        const string sql = @"
                INSERT INTO Users (Id, Name, Email, LastEventSynced)
                VALUES (@Id, @Name, @Email, @LastEventSynced);";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Email,
            LastEventSynced = entity.LastEventSynced.ToString()
        });

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        return await DeleteByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Users WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { Id = id });

        return affected > 0;
    }

    public async Task<IEnumerable<User>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Users;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var users = await connection.QueryAsync<User>(sql);

        // Convert string LastEventSynced to Guid
        foreach (var user in users)
        {
            if (Guid.TryParse(user.LastEventSynced.ToString(), out Guid lastEventSynced))
            {
                user.LastEventSynced = lastEventSynced;
            }
            else
            {
                user.LastEventSynced = Guid.Empty;
            }
        }

        return users;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Users WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var user = await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });

        if (user != null)
        {
            // Convert string LastEventSynced to Guid
            if (Guid.TryParse(user.LastEventSynced.ToString(), out Guid lastEventSynced))
            {
                user.LastEventSynced = lastEventSynced;
            }
            else
            {
                user.LastEventSynced = Guid.Empty;
            }
        }

        return user;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Users WHERE Email = @Email;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var user = await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });

        if (user != null)
        {
            // Convert string LastEventSynced to Guid
            if (Guid.TryParse(user.LastEventSynced.ToString(), out Guid lastEventSynced))
            {
                user.LastEventSynced = lastEventSynced;
            }
            else
            {
                user.LastEventSynced = Guid.Empty;
            }
        }

        return user;
    }

    public IQueryable<User> Queryable()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var users = connection.Query<User>("SELECT * FROM Users").AsQueryable();

        // Convert string LastEventSynced to Guid
        foreach (var user in users)
        {
            if (Guid.TryParse(user.LastEventSynced.ToString(), out Guid lastEventSynced))
            {
                user.LastEventSynced = lastEventSynced;
            }
            else
            {
                user.LastEventSynced = Guid.Empty;
            }
        }

        return users;
    }

    public async Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
                UPDATE Users
                SET Name = @Name, 
                    Email = @Email,
                    LastEventSynced = @LastEventSynced
                WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name,
            entity.Email,
            LastEventSynced = entity.LastEventSynced.ToString()
        });

        return affected > 0;
    }

    public async Task<bool> UpdateLastEventSyncedAsync(Guid userId, Guid eventId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
                UPDATE Users
                SET LastEventSynced = @LastEventSynced
                WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            Id = userId,
            LastEventSynced = eventId.ToString()
        });

        return affected > 0;
    }
}
