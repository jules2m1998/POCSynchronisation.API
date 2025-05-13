using Microsoft.Extensions.Logging;

namespace Infrastructure.Dapper;

/// <summary>
/// Handles database initialization by automatically discovering and calling all registered IModelInitialiser implementations
/// </summary>
public class DatabaseInitializer(
    ILogger<DatabaseInitializer> logger,
    IPermissionManger permissionManger,
    IServiceProvider serviceProvider)
{
    private readonly ILogger<DatabaseInitializer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Initializes the database by identifying all services that implement IModelInitialiser 
    /// and running their initialization methods
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        _logger.LogInformation("Starting database initialization");
        var permissionGranted = await permissionManger.CheckAndRequestStoragePermission();
        if (!permissionGranted) return;

        try
        {
            // Get all registered repository instances from the service provider
            // and filter those that implement IModelInitialiser
            var initializerTypes = _serviceProvider
                .GetServices<IModelInitialiser>()
                .Distinct(new ModelInitialiserComparer())
                .ToList();

            _logger.LogInformation("Found {Count} unique model initializers to execute", initializerTypes.Count);

            // Execute all initializers
            foreach (var initializer in initializerTypes)
            {
                var initializerType = initializer!.GetType().Name;
                _logger.LogInformation("Executing initializer: {InitializerName}", initializerType);

                try
                {
                    await initializer.InitializeAsync();
                    _logger.LogInformation("Successfully executed initializer: {InitializerName}", initializerType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing initializer {InitializerName}: {ErrorMessage}",
                        initializerType, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Comparer to ensure we don't run the same initializer twice
    /// </summary>
    private class ModelInitialiserComparer : IEqualityComparer<IModelInitialiser?>
    {
        public bool Equals(IModelInitialiser? x, IModelInitialiser? y)
        {
            if (x == null || y == null)
                return false;

            return x.GetType() == y.GetType();
        }

        public int GetHashCode(IModelInitialiser obj)
        {
            return obj.GetType().GetHashCode();
        }
    }
}