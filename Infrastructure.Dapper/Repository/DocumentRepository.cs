using Dapper;

namespace Infrastructure.Dapper.Repository;

public class DocumentRepository(IDbConnectionFactory dbConnectionFactory) : BaserRepository<Document, Guid>(dbConnectionFactory), IBaseRepository<Document, Guid>, IBaserepositoryWithInitialisation<Document, Guid>
{
    const string initializationSql = """
        CREATE TABLE IF NOT EXISTS Documents (
            Id TEXT NOT NULL,
            FileName TEXT NOT NULL,
            StorageUrl TEXT NOT NULL,
            CreatedAt DATETIME NOT NULL,
            ModifiedAt DATETIME,
            IsSynced INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT PK_Documents PRIMARY KEY (Id)
        );
    """;

    const string sqlInsertion = """
        INSERT INTO Documents (Id, FileName, StorageUrl, CreatedAt, ModifiedAt, IsSynced)
        VALUES (@Id, @FileName, @StorageUrl, @CreatedAt, @ModifiedAt, @IsSynced);
    """;


    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        await connection.ExecuteAsync(initializationSql);
    }

    public override async Task<bool> AddAsync(Document entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var result = await connection.ExecuteAsync(sqlInsertion, new
        {
            entity.Id,
            entity.FileName,
            entity.StorageUrl,
            entity.CreatedAt,
            entity.ModifiedAt,
            IsSynced = 0
        });

        return result > 0;
    }

}
