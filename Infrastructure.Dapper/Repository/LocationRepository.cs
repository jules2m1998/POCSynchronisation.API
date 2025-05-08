using Dapper;

namespace Infrastructure.Dapper.Repository;

public class LocationRepository(IDbConnectionFactory dbConnectionFactory) : IBaseRepository<Location, Guid>
{
    private readonly IDbConnectionFactory _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));

    public async Task<bool> AddAsync(Location entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO Locations (Id, Name)
            VALUES (@Id, @Name);";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name
        });

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Location entity, CancellationToken cancellationToken = default)
    {
        return await DeleteByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Locations WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { Id = id.ToString() });

        return affected > 0;
    }

    public async Task<IEnumerable<Location>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Locations;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var locations = await connection.QueryAsync<Location>(sql);

        return locations;
    }

    public async Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Locations WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var location = await connection.QuerySingleOrDefaultAsync<Location>(sql, new { Id = id.ToString() });

        return location;
    }

    public IQueryable<Location> Queryable()
    {
        var connection = _dbConnectionFactory.CreateConnection();
        var locations = connection.Query<Location>("SELECT * FROM Locations;").AsQueryable();

        return locations;
    }

    public async Task<bool> UpdateAsync(Location entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Locations
            SET Name = @Name
            WHERE Id = @Id;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            entity.Id,
            entity.Name
        });

        return affected > 0;
    }
}