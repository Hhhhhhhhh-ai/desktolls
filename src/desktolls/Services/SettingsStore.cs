using System.IO;
using System.Text.Json;
using DeskTolls.Models;

namespace DeskTolls.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "desktolls");

    public static string SettingsPath { get; } = Path.Combine(SettingsDirectory, "settings.json");

    public (AppSettings Settings, bool IsFirstRun) Load()
    {
        if (!File.Exists(SettingsPath))
        {
            var initialSettings = new AppSettings();
            Save(initialSettings);
            return (initialSettings, true);
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return (settings, false);
        }
        catch (JsonException)
        {
            var backupPath = SettingsPath + $".invalid-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(SettingsPath, backupPath, true);

            var settings = new AppSettings();
            Save(settings);
            return (settings, true);
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(SettingsDirectory);

        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsPath, true);
    }
}
