using Dapper;
using Dommel;

namespace Infrastructure.Dapper.Repository;

public class PackageRepository(IDbConnectionFactory dbConnectionFactory)
    : BaserRepository<Package, Guid>(dbConnectionFactory),
    IBaserepositoryWithInitialisation<Package, Guid>

{
    public override async Task<bool> AddAsync(Package entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.CreateVersion7();

        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = OFF;");

        string sqlWithLocation = @"
        INSERT INTO Packages 
            (Id, Reference, Weight, Volume, TareWeight, CreatedAt, LocationId, ConflictOfId)
        VALUES 
            (@Id, @Reference, @Weight, @Volume, @TareWeight, @CreatedAt, @LocationId, @ConflictOfId);";

        string sqlWithoutLocation = @"
        INSERT INTO Packages 
            (Id, Reference, Weight, Volume, TareWeight, CreatedAt, ConflictOfId)
        VALUES 
            (@Id, @Reference, @Weight, @Volume, @TareWeight, @CreatedAt, @ConflictOfId);";

        var parameters = new
        {
            entity.Id,
            entity.Reference,
            entity.Weight,
            entity.Volume,
            entity.TareWeight,
            entity.CreatedAt,
            LocationId = entity.Location?.Id,
            entity.ConflictOfId
        };

        var sql = entity.Location?.Id != null
            ? sqlWithLocation
            : sqlWithoutLocation;

        var affected = await connection.ExecuteAsync(sql, parameters);
        return affected > 0;
    }



    public override async Task<IEnumerable<Package>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        return await connection.GetAllAsync<Package, Location, Package>();
    }

    public override async Task<Package?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        return await connection.GetAsync<Package, Location, Package>(id);
    }

    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        await connection.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS Locations (
                    Id TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL
                );
            """);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Packages (
                Id TEXT NOT NULL PRIMARY KEY,
                Reference TEXT NOT NULL,
                Weight REAL,
                Volume REAL,
                TareWeight REAL,
                CreatedAt TEXT NOT NULL,
                LocationId TEXT NULL,
                ConflictOfId TEXT,
                FOREIGN KEY (LocationId) REFERENCES Locations(Id),
                FOREIGN KEY (ConflictOfId) REFERENCES Packages(Id)
            );
            """);
    }


}