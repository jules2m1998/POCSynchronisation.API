using SQLite;
using System.Linq.Expressions;

namespace SQLiteManager.Abstractions;

// Interface for the database context
public interface IAppDbContext : IDisposable
{
    SQLiteConnection Connection { get; }
    Task InitializeAsync();
    Task IncludeAsync<T, TProperty>(T entity, Expression<Func<T, TProperty>> navigationProperty);
}