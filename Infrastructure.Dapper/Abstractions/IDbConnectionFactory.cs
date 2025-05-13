using System.Data;

namespace Infrastructure.Dapper.Abstractions;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
