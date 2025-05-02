using Poc.Synchronisation.Domain.Abstractions;

namespace Infrastructure.Dapper.Abstractions;

public interface IBaserepositoryWithInitialisation<TEntity, TId> :
    IBaseRepository<TEntity, TId>, IModelInitialiser 
    where TEntity : class, new();
