using System.Data;

namespace Infrastructure.Dapper.Abstractions;

public interface IDbConnectionFactory
{
    public string ConnectionString { get; }
    IDbConnection CreateConnection();
    void CleanDb();
}
