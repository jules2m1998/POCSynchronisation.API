using Dapper;
using Poc.Synchronisation.Domain.Abstractions.Repositories;

namespace Infrastructure.Dapper.Repository;

public class SynchronisationRepository(IDbConnectionFactory dbConnectionFactory)
    : ISynchronisationRepository
{
    const string sqlGetCountNonSyncFilePaths = """
        SELECT COUNT(*)
        FROM Documents
        WHERE IsSynced = 0
    """;

    const string sqlGetNonSyncDataCount = """
        SELECT COUNT(*)
        FROM StoredEvents
    """;


    public async Task<int> GetCountOfDocumentsToSync()
    {
        var connection = dbConnectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<int>(sqlGetCountNonSyncFilePaths);
    }

    public async Task<int> GetCountOfEventsToSynchronise()
    {
        var connection = dbConnectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<int>(sqlGetNonSyncDataCount);
    }
}
