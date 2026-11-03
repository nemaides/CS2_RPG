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

        EnsureColumn(dbConnection, "Prestige", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(dbConnection, "SelectedTitle", "TEXT NOT NULL DEFAULT ''");

        const string questTableQuery = @"
            CREATE TABLE IF NOT EXISTS player_quests (
                SteamId INTEGER NOT NULL,
                QuestId TEXT NOT NULL,
                PeriodKey TEXT NOT NULL,
                Progress INTEGER NOT NULL DEFAULT 0,
                Claimed INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (SteamId, QuestId)
            );";
        using (var command = new SqliteCommand(questTableQuery, dbConnection))
        {
            command.ExecuteNonQuery();
        }

        Console.WriteLine("[CS2 RPG] SQLite Database initialized successfully with dynamic SkillData support.");

        return dbConnection;
    }

    private static void EnsureColumn(SqliteConnection connection, string columnName, string definition)
    {
        bool exists = false;
        using (var check = new SqliteCommand("PRAGMA table_info(players);", connection))
        using (var reader = check.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists) return;
        using var alter = new SqliteCommand($"ALTER TABLE players ADD COLUMN {columnName} {definition};", connection);
        alter.ExecuteNonQuery();
    }
}