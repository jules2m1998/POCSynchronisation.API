using Dapper;
using Dommel;

namespace Infrastructure.Dapper.Repository;

public class PackageRepository(IDbConnectionFactory dbConnectionFactory)
    : BaserRepository<Package, Guid>(dbConnectionFactory),
    IBaserepositoryWithInitialisation<Package, Guid>

{
    private Package EncryptPackageData(Package package)
    {
        ArgumentNullException.ThrowIfNull(package, nameof(package));

        var encryptedPackage = new Package
        {
            Id = package.Id,
            Reference = DataEncryption.Encrypt(package.Reference), // Encrypt sensitive reference
            Weight = package.Weight,
            Volume = package.Volume,
            TareWeight = package.TareWeight,
            CreatedAt = package.CreatedAt,
            Location = package.Location, // Location might need separate encryption
            ConflictOfId = package.ConflictOfId
        };

        return encryptedPackage;
    }

    private Package DecryptPackageData(Package package)
    {
        ArgumentNullException.ThrowIfNull(package, nameof(package));

        var decryptedPackage = new Package
        {
            Id = package.Id,
            Reference = DataEncryption.Decrypt(package.Reference), // Decrypt sensitive reference
            Weight = package.Weight,
            Volume = package.Volume,
            TareWeight = package.TareWeight,
            CreatedAt = package.CreatedAt,
            Location = package.Location,
            ConflictOfId = package.ConflictOfId
        };

        return decryptedPackage;
    }

    private IEnumerable<Package> DecryptPackageData(IEnumerable<Package> packages)
    {
        return packages?.Select(DecryptPackageData) ?? Enumerable.Empty<Package>();
    }

    public override async Task<bool> AddAsync(Package entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
            entity.Id = Guid.CreateVersion7();

        // Encrypt the entity before storing
        var encryptedEntity = EncryptPackageData(entity);

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
            encryptedEntity.Id,
            encryptedEntity.Reference, // This is now encrypted
            encryptedEntity.Weight,
            encryptedEntity.Volume,
            encryptedEntity.TareWeight,
            encryptedEntity.CreatedAt,
            LocationId = encryptedEntity.Location?.Id,
            encryptedEntity.ConflictOfId
        };

        var sql = encryptedEntity.Location?.Id != null
            ? sqlWithLocation
            : sqlWithoutLocation;

        var affected = await connection.ExecuteAsync(sql, parameters);
        return affected > 0;
    }

    public override async Task<IEnumerable<Package>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var packages = await connection.GetAllAsync<Package, Location, Package>();

        // Decrypt the retrieved packages
        return DecryptPackageData(packages);
    }

    public override async Task<Package?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var package = await connection.GetAsync<Package, Location, Package>(id);

        // Decrypt the retrieved package
        return package != null ? DecryptPackageData(package) : null;
    }


    public override async Task<bool> UpdateAsync(Package entity, CancellationToken cancellationToken = default)
    {
        // Encrypt the entity before updating
        var encryptedEntity = EncryptPackageData(entity);

        using var connection = _dbConnectionFactory.CreateConnection();

        string sql = @"
        UPDATE Packages 
        SET Reference = @Reference,
            Weight = @Weight,
            Volume = @Volume,
            TareWeight = @TareWeight,
            LocationId = @LocationId,
            ConflictOfId = @ConflictOfId
        WHERE Id = @Id";

        var parameters = new
        {
            encryptedEntity.Id,
            encryptedEntity.Reference, // This is now encrypted
            encryptedEntity.Weight,
            encryptedEntity.Volume,
            encryptedEntity.TareWeight,
            LocationId = encryptedEntity.Location?.Id,
            encryptedEntity.ConflictOfId
        };

        var affected = await connection.ExecuteAsync(sql, parameters);
        return affected > 0;
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