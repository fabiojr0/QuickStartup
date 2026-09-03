using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using QuickStartup.Models;

namespace QuickStartup.Services;

public class ProfileService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickStartup");

    private static readonly string ProfilesFile =
        Path.Combine(AppDataDir, "profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public ObservableCollection<Profile> Profiles { get; } = new();

    public ProfileService()
    {
        Directory.CreateDirectory(AppDataDir);
        Load();
    }

    public void Load()
    {
        Profiles.Clear();
        if (!File.Exists(ProfilesFile)) return;

        try
        {
            var json = File.ReadAllText(ProfilesFile);
            var list = JsonSerializer.Deserialize<List<Profile>>(json, JsonOptions);
            if (list is null) return;

            foreach (var p in list)
                Profiles.Add(p);
        }
        catch
        {
            // Arquivo corrompido — ignora e começa do zero
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Profiles.ToList(), JsonOptions);
        File.WriteAllText(ProfilesFile, json);
    }

    public void Add(Profile profile)
    {
        Profiles.Add(profile);
        Save();
    }

    public void Remove(Profile profile)
    {
        Profiles.Remove(profile);
        Save();
    }

    public void Duplicate(Profile profile)
    {
        var clone = profile.Clone();
        Profiles.Add(clone);
        Save();
    }

    public void SetDefault(Profile? profile)
    {
        foreach (var p in Profiles)
            p.IsDefault = (p == profile);
        Save();
    }

    public Profile? GetDefault() => Profiles.FirstOrDefault(p => p.IsDefault);
}
