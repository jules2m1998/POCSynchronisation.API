using Infrastructure.Dapper.Services.EventIdUpdaters;
using Infrastructure.Dapper.Services.Generated;
using Poc.Synchronisation.Domain.Abstractions.Repositories;

namespace Infrastructure.Dapper;

public static class DependecyInjection
{
    public async static Task<IServiceCollection> AddInfrastructure(this IServiceCollection services, string dbPath, string password, string apiUrl)
    {
        services.AddSingleton<IDbConnectionFactory>(provider =>
        {
            var connection = new SqliteConnectionFactory(dbPath, password);
            return connection;
        });

        services.AddScoped<IBaseRepository<Location, Guid>, LocationRepository>();
        services.AddScoped<IBaseRepository<Package, Guid>, PackageRepository>();
        services.AddScoped<IBaseRepository<StoredEvent, Guid>, StoredEventRepository>();
        services.AddScoped<IBaseRepository<User, Guid>, UserRepository>();
        services.AddScoped<IBaseRepository<Document, Guid>, DocumentRepository>();
        services.AddScoped<IBaseRepository<PackageDocument, Guid>, PackageDocumentRepository>();
        services.AddScoped<IPackagerDocumentRepository, PackageDocumentRepository>();

        services.AddScoped<IModelInitialiser, PackageRepository>();
        services.AddScoped<IModelInitialiser, StoredEventRepository>();
        services.AddScoped<IModelInitialiser, UserRepository>();
        services.AddScoped<IModelInitialiser, PackageDocumentRepository>();
        services.AddScoped<IModelInitialiser, DocumentRepository>();


        services.AddScoped<IEventIdUpdater, CreatePackageIdUpdater>();

        services.AddScoped<DatabaseInitializer>();

        services.AddRefitClient<IApi>().ConfigureHttpClient(c =>
        {
            c.BaseAddress = new Uri(apiUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        return services;
    }
}

