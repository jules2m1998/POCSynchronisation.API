using Dapper;

namespace Infrastructure.Dapper.Repository;

public class DocumentRepository(IDbConnectionFactory dbConnectionFactory) : BaserRepository<Document, Guid>(dbConnectionFactory), IBaseRepository<Document, Guid>, IBaserepositoryWithInitialisation<Document, Guid>
{
    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS Documents (
                    Id TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    StorageUrl TEXT NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    ModifiedAt DATETIME,
                    CONSTRAINT PK_Documents PRIMARY KEY (Id)
                );
            """);
    }
}
