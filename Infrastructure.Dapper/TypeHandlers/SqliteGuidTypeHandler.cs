using Dapper;
using System.Data;

namespace Infrastructure.Dapper.TypeHandlers;

public class SqliteGuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid guid)
    {
        parameter.Value = guid.ToString();
    }

    public override Guid Parse(object value)
    {
        if (value == null) return Guid.Empty;

        // SQLite might store as TEXT or BLOB
        if (value is byte[] bytes)
        {
            return new Guid(bytes);
        }

        return Guid.Parse(value.ToString());
    }
}