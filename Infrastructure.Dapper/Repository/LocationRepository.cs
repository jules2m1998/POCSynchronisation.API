using Dapper;

namespace Infrastructure.Dapper.Repository;

public class LocationRepository(IDbConnectionFactory dbConnectionFactory) :
    BaserRepository<Location, Guid>(dbConnectionFactory),
    IBaseRepository<Location, Guid>
{
    public override async Task<bool> AddAsync(Location entity, CancellationToken cancellationToken = default)
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

}