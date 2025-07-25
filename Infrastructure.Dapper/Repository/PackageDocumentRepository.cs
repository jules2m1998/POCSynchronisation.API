using Dapper;
using Poc.Synchronisation.Domain.Abstractions.Repositories;

namespace Infrastructure.Dapper.Repository;

public class PackageDocumentRepository(IDbConnectionFactory dbConnectionFactory)
    : BaserRepository<PackageDocument, Guid>(dbConnectionFactory),
    IPackagerDocumentRepository,
    IBaserepositoryWithInitialisation<PackageDocument, Guid>
{
    #region Sql Queries
    private const string InitialisationQuery = """
        CREATE TABLE IF NOT EXISTS PackageDocuments (
            PackageId INTEGER NOT NULL,
            DocumentId INTEGER NOT NULL,
            UNIQUE(PackageId, DocumentId),
            FOREIGN KEY (PackageId) REFERENCES Packages(Id) ON DELETE CASCADE,
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
        );
        """;

    private const string InsertDocumentSql = """
        INSERT INTO Documents (Id, FileName, StorageUrl, CreatedAt, ModifiedAt)
        VALUES (@Id, @FileName, @StorageUrl, @CreatedAt, @ModifiedAt);
        """;

    private const string InsertLinkSql = """
        INSERT OR IGNORE INTO PackageDocuments (PackageId, DocumentId)
        VALUES (@PackageId, @DocumentId);
        """;

    private const string GetByPackageIdSql = """
        SELECT
            pd.PackageId,
            pd.DocumentId,
            p.Id,
            p.Reference,
            p.Weight,
            p.Volume,
            p.TareWeight,
            d.Id,
            d.FileName,
            d.StorageUrl
        FROM PackageDocuments pd
        INNER JOIN Packages p ON pd.PackageId = p.Id
        INNER JOIN Documents d ON pd.DocumentId = d.Id
        WHERE pd.PackageId = @PackageId;
        """;

    private const string DeletePackageDocsSql = """
        PRAGMA foreign_keys = ON;
        DELETE FROM Documents
        WHERE Id IN (
            SELECT DocumentId
            FROM PackageDocuments
            WHERE PackageId = @PackageId
        );
        """;

    private const string DeleteOneByIdsSql = """
        DELETE FROM PackageDocuments
        WHERE DocumentId = @DocumentId and PackageId = @PackageId;
        """;
    #endregion


    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = OFF;");
        await connection.ExecuteAsync(InitialisationQuery);
    }

    public async Task<IReadOnlyCollection<PackageDocument>> GetByPackageIdAsync(Guid packageId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var result = await connection.QueryAsync<PackageDocument, Package, Document, PackageDocument>(
            GetByPackageIdSql,
            (packageDocument, package, document) =>
            {
                packageDocument.Package = package;
                packageDocument.Document = document;
                packageDocument.PackageId = package.Id;
                packageDocument.DocumentId = document.Id;
                return packageDocument;
            },
            new { PackageId = packageId },
            splitOn: "Id,Id"
        );

        return result.ToList().AsReadOnly();
    }

    public override async Task<bool> AddAsync(PackageDocument entity, CancellationToken cancellationToken = default)
    {
        if (entity?.Document == null && entity?.DocumentId == null)
            throw new ArgumentNullException(nameof(entity), "Entity or Document cannot be null");

        var documentId = entity.DocumentId;

        using var connection = _dbConnectionFactory.CreateConnection();

        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync("PRAGMA foreign_keys = ON;", transaction: transaction);

            if (entity.Document is not null)
            {
                var documentParams = new
                {
                    entity.Document.Id,
                    entity.Document.FileName,
                    entity.Document.StorageUrl,
                    entity.Document.CreatedAt,
                    entity.Document.ModifiedAt
                };

                var documentInserted = await connection.ExecuteAsync(
                    InsertDocumentSql,
                    documentParams,
                    transaction: transaction
                );

                if (documentInserted == 0)
                    throw new InvalidOperationException("Failed to insert document");

                documentId = entity.Document.Id;
            }

            var linkInserted = await connection.ExecuteAsync(
                InsertLinkSql,
                new { entity.PackageId, DocumentId = documentId },
                transaction: transaction
            );

            transaction.Commit();
            return linkInserted > 0;
        }
        catch
        {
            transaction.Commit();
            throw;
        }
    }

    public override async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {

        using var connection = _dbConnectionFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(
            DeletePackageDocsSql,
            new { PackageId = id }
        );

        return affected > 0;
    }

    public async Task<bool> DeleteOneByIdAsync(Guid packageId, Guid documentId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(
            DeleteOneByIdsSql,
            new { PackageId = packageId, DocumentId = documentId }
        );
        return affected > 0;
    }
}
