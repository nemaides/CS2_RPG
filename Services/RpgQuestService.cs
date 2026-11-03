using System.Globalization;
using CounterStrikeSharp.API.Core;
using CS2_RPG.Models;
using Microsoft.Data.Sqlite;

namespace CS2_RPG.Services;

public static class RpgQuestService
{
    private sealed record QuestDefinition(string Id, string Title, bool Weekly, int Target, int Reward);

    public static void RecordDailyKill(SqliteConnection? db, RpgPlayer player, CCSPlayerController controller, RpgConfig config) =>
        RecordProgress(db, player, controller, "daily_kills", false, config.DailyKillQuestTarget, config.DailyKillQuestXP,
            $"Get {config.DailyKillQuestTarget} player kills");

    public static void RecordDailyHeadshot(SqliteConnection? db, RpgPlayer player, CCSPlayerController controller, RpgConfig config) =>
        RecordProgress(db, player, controller, "daily_headshots", false, config.DailyHeadshotQuestTarget, config.DailyHeadshotQuestXP,
            $"Get {config.DailyHeadshotQuestTarget} headshot kills");

    public static void RecordWeeklyAssist(SqliteConnection? db, RpgPlayer player, CCSPlayerController controller, RpgConfig config) =>
        RecordProgress(db, player, controller, "weekly_assists", true, config.WeeklyAssistQuestTarget, config.WeeklyAssistQuestXP,
            $"Get {config.WeeklyAssistQuestTarget} assists");

    public static void RecordWeeklyWin(SqliteConnection? db, RpgPlayer player, CCSPlayerController controller, RpgConfig config) =>
        RecordProgress(db, player, controller, "weekly_wins", true, config.WeeklyWinQuestTarget, config.WeeklyWinQuestXP,
            $"Win {config.WeeklyWinQuestTarget} rounds");

    public static void ShowQuests(CCSPlayerController controller, RpgPlayer player, SqliteConnection? db, RpgConfig config)
    {
        if (db == null)
        {
            controller.PrintToChat(" \x02[RPG]\x01 Quest progress is unavailable because the database is offline.");
            return;
        }

        controller.PrintToChat("\x04--- [ RPG Quests | reset on UTC periods ] ---");
        foreach (var quest in GetQuests(config))
        {
            if (quest.Target <= 0 || quest.Reward <= 0)
            {
                controller.PrintToChat($"\x01{(quest.Weekly ? "Weekly" : "Daily")}: {quest.Title} - Disabled");
                continue;
            }

            var (_, progress, claimed) = ReadProgress(db, player.SteamId, quest);
            string periodLabel = quest.Weekly ? "Weekly" : "Daily";
            string status = claimed ? " \x04[COMPLETE]\x01" : string.Empty;
            controller.PrintToChat($"\x01{periodLabel}: {quest.Title} - {progress}/{quest.Target} | Reward: {quest.Reward} XP{status}");
        }
    }

    private static void RecordProgress(SqliteConnection? db, RpgPlayer player, CCSPlayerController controller, string questId,
        bool weekly, int target, int reward, string title)
    {
        if (db == null || target <= 0 || reward <= 0) return;

        string periodKey = GetPeriodKey(weekly);
        bool newlyClaimed;
        using (var transaction = db.BeginTransaction())
        {
            int previousProgress = 0;
            bool samePeriod = false;
            using (var select = new SqliteCommand(
                "SELECT PeriodKey, Progress FROM player_quests WHERE SteamId = @steamId AND QuestId = @questId;",
                db, transaction))
            {
                select.Parameters.AddWithValue("@steamId", (long)player.SteamId);
                select.Parameters.AddWithValue("@questId", questId);
                using var reader = select.ExecuteReader();
                if (reader.Read() && string.Equals(reader.GetString(0), periodKey, StringComparison.Ordinal))
                {
                    samePeriod = true;
                    previousProgress = reader.GetInt32(1);
                }
            }

            int progress = Math.Min(target, (samePeriod ? previousProgress : 0) + 1);
            newlyClaimed = progress >= target && (!samePeriod || previousProgress < target);

            using var upsert = new SqliteCommand(@"
                INSERT INTO player_quests (SteamId, QuestId, PeriodKey, Progress, Claimed)
                VALUES (@steamId, @questId, @periodKey, @progress, @claimed)
                ON CONFLICT(SteamId, QuestId) DO UPDATE SET
                    PeriodKey = @periodKey,
                    Progress = @progress,
                    Claimed = @claimed;", db, transaction);
            upsert.Parameters.AddWithValue("@steamId", (long)player.SteamId);
            upsert.Parameters.AddWithValue("@questId", questId);
            upsert.Parameters.AddWithValue("@periodKey", periodKey);
            upsert.Parameters.AddWithValue("@progress", progress);
            upsert.Parameters.AddWithValue("@claimed", progress >= target ? 1 : 0);
            upsert.ExecuteNonQuery();
            transaction.Commit();
        }

        if (newlyClaimed)
        {
            player.AddXP(reward);
            player.SaveToDatabase(db);
            controller.PrintToChat($" \x04[RPG]\x01 Quest complete: {title}! +{reward} XP.");
        }
    }

    private static (string PeriodKey, int Progress, bool Claimed) ReadProgress(SqliteConnection db, ulong steamId, QuestDefinition quest)
    {
        string periodKey = GetPeriodKey(quest.Weekly);
        using var command = new SqliteCommand(
            "SELECT PeriodKey, Progress, Claimed FROM player_quests WHERE SteamId = @steamId AND QuestId = @questId;",
            db);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.Parameters.AddWithValue("@questId", quest.Id);
        using var reader = command.ExecuteReader();
        if (!reader.Read() || !string.Equals(reader.GetString(0), periodKey, StringComparison.Ordinal))
            return (periodKey, 0, false);

        return (periodKey, reader.GetInt32(1), reader.GetInt32(2) != 0);
    }

    private static IEnumerable<QuestDefinition> GetQuests(RpgConfig config)
    {
        yield return new("daily_kills", $"Get {config.DailyKillQuestTarget} player kills", false, config.DailyKillQuestTarget, config.DailyKillQuestXP);
        yield return new("daily_headshots", $"Get {config.DailyHeadshotQuestTarget} headshot kills", false, config.DailyHeadshotQuestTarget, config.DailyHeadshotQuestXP);
        yield return new("weekly_assists", $"Get {config.WeeklyAssistQuestTarget} assists", true, config.WeeklyAssistQuestTarget, config.WeeklyAssistQuestXP);
        yield return new("weekly_wins", $"Win {config.WeeklyWinQuestTarget} rounds", true, config.WeeklyWinQuestTarget, config.WeeklyWinQuestXP);
    }

    private static string GetPeriodKey(bool weekly)
    {
        DateTime utcDate = DateTime.UtcNow.Date;
        if (!weekly) return utcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        int year = ISOWeek.GetYear(utcDate);
        int week = ISOWeek.GetWeekOfYear(utcDate);
        return $"{year}-W{week:D2}";
    }
}
