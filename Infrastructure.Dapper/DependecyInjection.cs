using Infrastructure.Dapper.Abstractions;
using Infrastructure.Dapper.Persistence;
using Infrastructure.Dapper.Repository;
using Microsoft.Extensions.DependencyInjection;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Models;

namespace Infrastructure.Dapper;

public static class DependecyInjection
{
    public async static Task<IServiceCollection> AddInfrastructure(this IServiceCollection services, string dbPath, string password)
    {
        services.AddSingleton<IDbConnectionFactory>(provider =>
        {
            var connection = new SqliteConnectionFactory(dbPath, password);
            return connection;
        });

        services.AddScoped<IBaseRepository<Location, Guid>, LocationRepository>();
        services.AddScoped<IBaseRepository<Package, Guid>, PackageRepository>();
        services.AddScoped<IBaseRepository<StoredEvent, Guid>, StoredEventRepository>();

        services.AddScoped<IModelInitialiser, PackageRepository>();
        services.AddScoped<IModelInitialiser, StoredEventRepository>();

        services.AddScoped<DatabaseInitializer>();

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Initialize database
        using (var scope = serviceProvider.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeDatabaseAsync();
        }
        return services;
    }
}

