using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace CS2_RPG.Models;

public class RpgPlayer
{
    public ulong SteamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public int SkillPoints { get; set; } = 0;

    public Dictionary<string, int> SkillLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public RpgPlayer(ulong steamId, string name)
    {
        SteamId = steamId;
        Name = name;
    }

    public int GetRequiredXP()
    {
        return 100 + (Level * 50);
    }

    public bool AddXP(int amount)
    {
        XP += amount;
        bool leveledUp = false;
        int req = GetRequiredXP();

        while (XP >= req)
        {
            XP -= req;
            Level++;
            SkillPoints++;
            leveledUp = true;
            req = GetRequiredXP();
        }

        return leveledUp;
    }

    public int GetSkillLevel(string skillId)
    {
        return SkillLevels.TryGetValue(skillId, out int lvl) ? lvl : 0;
    }


    public void SetSkillLevel(string skillId, int level)
    {
        SkillLevels[skillId] = level;
    }

    public static RpgPlayer LoadFromDatabase(SqliteConnection dbConnection, ulong steamId, string playerName)
    {
        var player = new RpgPlayer(steamId, playerName);

        string query = "SELECT Level, XP, SkillPoints, SkillData FROM players WHERE SteamId = @steamId;";
        using var command = new SqliteCommand(query, dbConnection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            player.Level = reader.GetInt32(0);
            player.XP = reader.GetInt32(1);
            player.SkillPoints = reader.GetInt32(2);

            if (!reader.IsDBNull(3))
            {
                string jsonBlob = reader.GetString(3);
                try
                {
                    player.SkillLevels = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonBlob)
                                         ?? new(StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    player.SkillLevels = new(StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        else
        {
            player.SaveToDatabase(dbConnection);
        }

        return player;
    }

    public void SaveToDatabase(SqliteConnection dbConnection)
    {
        string jsonBlob = JsonSerializer.Serialize(SkillLevels);

        string query = @"
            INSERT INTO players (SteamId, Name, Level, XP, SkillPoints, SkillData)
            VALUES (@steamId, @name, @level, @xp, @points, @skillData)
            ON CONFLICT(SteamId) DO UPDATE SET
                Name = @name,
                Level = @level,
                XP = @xp,
                SkillPoints = @points,
                SkillData = @skillData;";

        using var command = new SqliteCommand(query, dbConnection);
        command.Parameters.AddWithValue("@steamId", (long)SteamId);
        command.Parameters.AddWithValue("@name", Name);
        command.Parameters.AddWithValue("@level", Level);
        command.Parameters.AddWithValue("@xp", XP);
        command.Parameters.AddWithValue("@points", SkillPoints);
        command.Parameters.AddWithValue("@skillData", jsonBlob);
        command.ExecuteNonQuery();
    }
}