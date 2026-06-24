using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using CodingTracker.Config;
using CodingTracker.Models;

namespace CodingTracker.Database;

internal static class DatabaseManager
{
    static SqliteConnection Connection()
    {
        var connection = new SqliteConnection(AppConfig.ConnectionString);
        connection.Open();
        return connection;
    }

    public static void Init()
    {
        using var connection = Connection();
        connection.Execute(
            """
            CREATE TABLE IF NOT EXISTS coding_sessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL);
            """);
    }

    public static void Add(DateTime startTime, DateTime endTime)
    {
        using var connection = Connection();
        connection.Execute(
            "INSERT INTO coding_sessions (StartTime, EndTime) VALUES (@startTime, @endTime);",
            new { startTime, endTime });
    }

    public static void Update(int id, DateTime startTime, DateTime endTime)
    {
        using var connection = Connection();
        connection.Execute(
            "UPDATE coding_sessions SET StartTime = @startTime, EndTime = @endTime WHERE Id = @id;",
            new { id, startTime, endTime });
    }

    public static void Delete(int id)
    {
        using var connection = Connection();
        connection.Execute("DELETE FROM coding_sessions WHERE Id = @id;", new { id });
    }

    public static bool Exists(int id)
    {
        using var connection = Connection();
        return connection.ExecuteScalar<long>(
            "SELECT COUNT(1) FROM coding_sessions WHERE Id = @id;", new { id }) > 0;
    }

    public static List<CodingSession> All()
    {
        using var connection = Connection();
        // SQLite stores these columns as TEXT/INTEGER; map explicitly so we don't rely on
        // Dapper coercing a TEXT column into DateTime via the record constructor.
        return connection.Query(
            "SELECT Id, StartTime, EndTime FROM coding_sessions ORDER BY StartTime DESC;")
            .Select(r => new CodingSession(
                Convert.ToInt32(r.Id),
                DateTime.Parse((string)r.StartTime, CultureInfo.InvariantCulture),
                DateTime.Parse((string)r.EndTime, CultureInfo.InvariantCulture)))
            .ToList();
    }
}
