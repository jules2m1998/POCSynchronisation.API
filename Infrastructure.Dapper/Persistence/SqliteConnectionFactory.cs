using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace Infrastructure.Dapper.Persistence;

public class SqliteConnectionFactory : IDbConnectionFactory, IDisposable
{
    private readonly string _connectionString;
    private IDbConnection? _connection;
    private readonly string pass = string.Empty;
    private bool _initialized = false;
    private readonly object _lock = new();
    private readonly string _dbPath;

    public string ConnectionString => _dbPath;

    public SqliteConnectionFactory(string dbPath, string password)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        pass = password;
        _dbPath = dbPath;
    }

    public IDbConnection CreateConnection()
    {
        if (_connection != null && _initialized)
        {
            // Return existing connection if it's still valid
            if (_connection is SqliteConnection sqliteConnection &&
                sqliteConnection.State != ConnectionState.Closed &&
                sqliteConnection.State != ConnectionState.Broken)
            {
                return _connection;
            }

            // If connection is closed or broken, dispose it first
            _connection.Dispose();
            _connection = null;
            _initialized = false;
        }

        lock (_lock)
        {
            if (_connection == null || !_initialized)
            {
                try
                {
                    _connection = new SqliteConnection(_connectionString);
                    _connection.Open();
                    var result = _connection.Execute($"PRAGMA key = '{pass}';");
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    _connection?.Dispose();
                    _connection = null;
                    _initialized = false;
                    throw new InvalidOperationException($"Failed to create SQLite connection: {ex.Message}", ex);
                }
            }

            return _connection;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
                _initialized = false;
            }
        }
        GC.SuppressFinalize(this);
    }

    public void CleanDb()
    {
        using var connection = CreateConnection();
        connection.Open();
        const string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";

        // For SQLite
        var tables = connection.Query<string>(sql);

        connection.Execute("PRAGMA foreign_keys = OFF");

        foreach (var table in tables)
        {
            connection.Execute($"DROP TABLE IF EXISTS [{table}]");
        }

        connection.Execute("PRAGMA foreign_keys = ON");
    }
}