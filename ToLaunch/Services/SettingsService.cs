using System;
using System.IO;
using System.Text.Json;
using ToLaunch.Models;

namespace ToLaunch.Services;

/// <summary>
/// Single point of access for the application data folder and the persisted <see cref="AppSettings"/>.
/// </summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>%LOCALAPPDATA%\ToLaunch</summary>
    public static string AppDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToLaunch");

    public static string ProfilesFolder => Path.Combine(AppDataFolder, "Profiles");

    public static string IconCacheFolder => Path.Combine(AppDataFolder, "Icons");

    public static string SettingsFilePath => Path.Combine(AppDataFolder, "settings.json");

    public static string ProfileFilePath(string profileName) =>
        Path.Combine(ProfilesFolder, $"{profileName}.json");

    /// <summary>
    /// Reads the persisted settings, returning defaults when the file is absent or unreadable.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, ReadOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to load app settings: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>Persists the settings. Returns false when the write failed.</summary>
    public static bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataFolder);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, WriteOptions));
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to save app settings: {ex.Message}");
            return false;
        }
    }

    public static string SerializeProfile(ProfileModel profile) =>
        JsonSerializer.Serialize(profile, WriteOptions);

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, ReadOptions);
}
