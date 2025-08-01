using Poc.Synchronisation.Domain.Abstractions.Services;

namespace Infrastructure.Dapper.Services;

public class AppGuards(IDbConnectionFactory dbConnection, IPermissionManger permission) : IAppGuards
{
    public bool DoesDbInitialized() =>
        !string.IsNullOrWhiteSpace(dbConnection.ConnectionString)
        && File.Exists(dbConnection.ConnectionString);
}
