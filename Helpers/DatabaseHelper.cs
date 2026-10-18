using Microsoft.Data.Sqlite;

namespace CS2_RPG.Helpers;

public static class DatabaseHelper
{
    public static SqliteConnection InitDatabase(string moduleDirectory)
    {
        string dbPath = Path.Combine(moduleDirectory, "rpg_players.db");
        var dbConnection = new SqliteConnection($"Data Source={dbPath}");
        dbConnection.Open();

        string tableQuery = @"
            CREATE TABLE IF NOT EXISTS players (
                SteamId INTEGER PRIMARY KEY,
                Name TEXT,
                Level INTEGER DEFAULT 1,
                XP INTEGER DEFAULT 0,
                SkillPoints INTEGER DEFAULT 0,
                SkillData TEXT
            );";

        using (var command = new SqliteCommand(tableQuery, dbConnection))
        {
            command.ExecuteNonQuery();
        }

        Console.WriteLine("[CS2 RPG] SQLite Database initialized successfully with dynamic SkillData support.");

        return dbConnection;
    }
}