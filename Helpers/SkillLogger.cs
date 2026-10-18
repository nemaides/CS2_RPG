using System.IO;

namespace CS2_RPG.Helpers;

public static class SkillLogger
{
    private static string? _pluginDir;

    public static void Initialize(string pluginDir)
    {
        _pluginDir = pluginDir;
    }

    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(_pluginDir)) return;

        try
        {
            string logPath = Path.Combine(_pluginDir, "rpg_skills.log");
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {

        }
    }
}