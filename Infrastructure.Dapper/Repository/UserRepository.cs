using Bogus;
using Dapper;
using Dommel;

namespace Infrastructure.Dapper.Repository;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid LastEventSynced { get; set; } = Guid.Empty;
    public bool IsInitialised { get; set; } = false;
}

public class UserRepository(IDbConnectionFactory dbConnectionFactory) :
    BaserRepository<User, Guid>(dbConnectionFactory),
    IBaserepositoryWithInitialisation<User, Guid>
{
    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        // Create the Users table if it doesn't exist
        var createTableSql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT NOT NULL UNIQUE,
                    LastEventSynced TEXT DEFAULT NULL,
                    IsInitialised INTEGER DEFAULT 0
                );
            ";
        await connection.ExecuteAsync(createTableSql);

        // Check if the IsInitialised column exists, and add it if it doesn't
        try
        {
            var checkColumnSql = "SELECT IsInitialised FROM Users LIMIT 1";
            await connection.ExecuteScalarAsync(checkColumnSql);
        }
        catch
        {
            // Column doesn't exist, add it
            var addColumnSql = "ALTER TABLE Users ADD COLUMN IsInitialised INTEGER DEFAULT 0";
            await connection.ExecuteAsync(addColumnSql);
        }

        // Check if any users exist
        var userCount = (await connection.GetAllAsync<User>()).Count();
        // Only create default user if none exist
        if (userCount == 0)
        {
            // Create a faker for generating a fake user
            var faker = new Faker<User>()
                .RuleFor(u => u.Id, f => Guid.CreateVersion7())
                .RuleFor(u => u.Name, f => f.Name.FullName())
                .RuleFor(u => u.Email, f => f.Internet.Email())
                .RuleFor(u => u.LastEventSynced, f => Guid.Empty)
                .RuleFor(u => u.IsInitialised, f => false);
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
}
