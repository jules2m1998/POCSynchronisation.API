using SQLite;
using SQLiteManager.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager;

public class AppDbContext : IAppDbContext
{
    private readonly SQLiteConnection _connection;
    private readonly Model _model;

    public SQLiteConnection Connection => _connection;

    // Constructor takes a SQLiteConnection and the list of IEntityTypeConfiguration instances
    public AppDbContext(SQLiteConnection connection, params object[] configurations)
    {
        _connection = connection;
        // Build the model by applying each configuration
        var modelBuilder = new ModelBuilder();
        foreach (var config in configurations)
        {
            // Use reflection to invoke config.Configure(builder)
            var configInterface = config.GetType().GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>));
            var entityType = configInterface.GetGenericArguments()[0];
            // Create EntityTypeBuilder<T> instance
            var builderType = typeof(EntityTypeBuilder<>).MakeGenericType(entityType);
            var builderInstance = Activator.CreateInstance(builderType, modelBuilder);

            // Call Configure(builderInstance)
            var configureMethod = configInterface.GetMethod("Configure");
            configureMethod!.Invoke(config, [builderInstance]);
        }
        _model = modelBuilder.Build();
    }

    // Initialize database: create tables and optionally seed data
    public Task InitializeAsync()
    {
        // Optionally enable foreign keys enforcement
        _connection.Execute("PRAGMA foreign_keys = ON;");
        // Create tables for each entity
        foreach (var entityMeta in _model.EntityTypes.Values)
        {
            _connection.CreateTable(entityMeta.EntityType, CreateFlags.AllImplicit);
        }
        return Task.CompletedTask;
    }

    // Manually load related data into a navigation property
    public async Task IncludeAsync<T, TProperty>(T entity, Expression<Func<T, TProperty>> navigationProperty)
    {
        // Ensure not called with null or uninitialized entity
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        // Extract the navigation property name
        string navName;
        if (navigationProperty.Body is MemberExpression memberExpr)
        {
            navName = memberExpr.Member.Name;
        }
        else if (navigationProperty.Body is UnaryExpression unaryExpr
                 && unaryExpr.Operand is MemberExpression unaryMember)
        {
            navName = unaryMember.Member.Name;
        }
        else
        {
            throw new ArgumentException("Invalid navigation expression");
        }

        // Determine if the navigation is a collection (IEnumerable) or reference
        var navProperty = typeof(T).GetProperty(navName);
        var isCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(navProperty.PropertyType)
                           && navProperty.PropertyType != typeof(string);

        var entityType = typeof(T);
        var entityMeta = _model.EntityTypes[entityType];

        if (isCollection)
        {
            // One-to-many: load collection for principal entity
            // Find the relationship where this type is principal with the given nav
            var rel = _model.Relationships
                .FirstOrDefault(r => r.PrincipalType == entityType && r.PrincipalNavigation == navName);
            if (rel == null) throw new InvalidOperationException($"No relationship for {entityType.Name}.{navName}");

            // Get the value of the principal key (e.g. User.Id)
            var keyValue = entityMeta.KeyProperty.GetValue(entity);

            // Query the dependent table: e.g. SELECT * FROM Post WHERE UserId = keyValue
            var tableName = rel.DependentType.Name;
            var fkName = rel.ForeignKeyProperty.Name;
            string sql = $"SELECT * FROM {tableName} WHERE {fkName} = ?";
            var queryMethod = typeof(SQLiteConnection).GetMethod("Query", new Type[] { typeof(string), typeof(object[]) })
                .MakeGenericMethod(rel.DependentType);
            var list = (System.Collections.IList)queryMethod.Invoke(_connection, new object[] { sql, new object[] { keyValue } });

            // Assign the list to the navigation property (assumes it's a List<TDependent>)
            navProperty.SetValue(entity, list);
        }
        else
        {
            // Many-to-one (reference): load single principal for dependent entity
            // Find the relationship where this type is dependent with the given nav
            var rel = _model.Relationships
                .FirstOrDefault(r => r.DependentType == entityType && r.DependentNavigation == navName);
            if (rel == null) throw new InvalidOperationException($"No relationship for {entityType.Name}.{navName}");

            // Get the foreign key value from the dependent (e.g. Post.UserId)
            var fkValue = rel.ForeignKeyProperty.GetValue(entity);
            // Use SQLiteConnection.Get<T>() to fetch the principal entity by PK
            var getMethod = typeof(SQLiteConnection).GetMethod("Get").MakeGenericMethod(rel.PrincipalType);
            var principalEntity = getMethod.Invoke(_connection, new object[] { fkValue });

            // Assign the loaded principal to the navigation property
            navProperty.SetValue(entity, principalEntity);
        }

        await Task.CompletedTask;
    }

    public void Dispose() => _connection?.Dispose();
}