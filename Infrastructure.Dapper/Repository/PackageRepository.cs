using Dapper;

namespace Infrastructure.Dapper.Repository;

public class PackageRepository(IDbConnectionFactory dbConnectionFactory) : IBaserepositoryWithInitialisation<Package, Guid>
{
    private readonly IDbConnectionFactory _dbConnectionFactory = 
        dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));

    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var sql = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Locations (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Packages (
                Id TEXT NOT NULL PRIMARY KEY,
                Reference TEXT NOT NULL,
                Weight REAL,
                Volume REAL,
                TareWeight REAL,
                CreatedAt TEXT NOT NULL,
                LocationId TEXT,
                FOREIGN KEY (LocationId) REFERENCES Locations(Id)
            );
        """;

        var result = await connection.ExecuteAsync(sql);
    }

    public async Task<bool> AddAsync(Package entity, CancellationToken cancellationToken = default)
    {
        // Ensure we have a valid ID
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.CreateVersion7();
        }

        const string sql = @"
            INSERT INTO Packages (Id, Reference, Weight, Volume, TareWeight, CreatedAt, LocationId)
            VALUES (@Id, @Reference, @Weight, @Volume, @TareWeight, @CreatedAt, @LocationId);";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Reference,
            entity.Weight,
            entity.Volume,
            entity.TareWeight,
            CreatedAt = entity.CreatedAt.ToString("o"),
            LocationId = entity.Location?.Id
        });

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Package entity, CancellationToken cancellationToken = default)
    {
        return await DeleteByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Packages WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { Id = id });

        return affected > 0;
    }

    public async Task<IEnumerable<Package>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT p.*, l.*
            FROM Packages p
            LEFT JOIN Locations l ON p.LocationId = l.Id;";

        using var connection = _dbConnectionFactory.CreateConnection();

        var packages = await connection.QueryAsync<Package, Location, Package>(
            sql,
            (package, location) =>
            {
                if (location != null)
                {
                    package.Location = location;
                }
                return package;
            },
            splitOn: "LocationId"
        );

        return packages;
    }

    public async Task<Package?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT p.*, l.*
            FROM Packages p
            LEFT JOIN Locations l ON p.LocationId = l.Id
            WHERE p.Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();

        var packages = await connection.QueryAsync<Package, Location, Package>(
            sql,
            (package, location) =>
            {
                if (location != null)
                {
                    package.Location = location;
                }
                return package;
            },
            new { Id = id },
            splitOn: "LocationId"
        );

        return packages.FirstOrDefault();
    }

    public IQueryable<Package> Queryable()
    {
        const string sql = @"
            SELECT p.*, l.*
            FROM Packages p
            LEFT JOIN Locations l ON p.LocationId = l.Id;";

        using var connection = _dbConnectionFactory.CreateConnection();

        var packages = connection.Query<Package, Location, Package>(
            sql,
            (package, location) =>
            {
                if (location != null)
                {
                    package.Location = location;
                }
                return package;
            },
            splitOn: "LocationId"
        ).AsQueryable();

        return packages;
    }

    public async Task<bool> UpdateAsync(Package entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Packages
            SET Reference = @Reference, 
                Weight = @Weight, 
                Volume = @Volume, 
                TareWeight = @TareWeight
            WHERE Id = @Id;";

        const string sqlWithoutLocation = @"
            UPDATE Packages
            SET Reference = @Reference, 
                Weight = @Weight, 
                Volume = @Volume, 
                TareWeight = @TareWeight,
                LocationId = @LocationId
            WHERE Id = @Id;";


        var LocationId = entity.LocationId?.ToString() ?? null;
        var query = LocationId is null ? sql : sqlWithoutLocation;

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(query, new
        {
            entity.Id,
            entity.Reference,
            entity.Weight,
            entity.Volume,
            entity.TareWeight,
            LocationId
        });

        return affected > 0;
    }
}