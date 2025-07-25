using Dapper;
using Poc.Synchronisation.Domain.Abstractions.Services;

namespace Infrastructure.Dapper.Services;

public class DBForeignKeyMode(IDbConnectionFactory dbConnectionFactory)
    : IDBForeignKeyMode
{
    public async Task Off()
    {
        using var connection = dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = OFF;");
    }

    public async Task On()
    {
        using var connection = dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
    }
}
