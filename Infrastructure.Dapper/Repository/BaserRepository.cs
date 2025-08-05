using Dommel;

namespace Infrastructure.Dapper.Repository;

public class BaserRepository<TEntity, TId>(IDbConnectionFactory dbConnectionFactory) :
    IBaseRepository<TEntity, TId> where TEntity : class, new() where TId : notnull
{
    protected readonly IDbConnectionFactory _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));

    public virtual async Task<bool> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var result = await connection.InsertAsync(entity, cancellationToken: cancellationToken);
        return result is not null;
    }

    public async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var result = await connection.DeleteAsync(entity, cancellationToken: cancellationToken);
        return result;
    }

    public virtual async Task<bool> DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var element = await connection.GetAsync<TEntity>(id, cancellationToken: cancellationToken);
        if (element is null)
        {
            return false;
        }
        var result = await connection.DeleteAsync(element, cancellationToken: cancellationToken);
        return result;
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        return await connection.GetAllAsync<TEntity>(cancellationToken: cancellationToken);
    }

    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        return await connection.GetAsync<TEntity>(id, cancellationToken: cancellationToken);
    }

    public IQueryable<TEntity> Queryable()
    {
        return GetAllAsync().Result.AsQueryable();
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var result = await connection.UpdateAsync(entity, cancellationToken: cancellationToken);
        return result;
    }
}
