using System.Text.Json;
using System.IO;
using DynamicIsland.Models;

namespace DynamicIsland.Services;

public sealed class ConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _configurationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIsland",
        "configuration.json");

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<AppConfiguration> LoadAsync()
    {
        try
        {
            if (!File.Exists(_configurationPath))
            {
                return new AppConfiguration();
            }

            await using var stream = File.OpenRead(_configurationPath);
            return await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, JsonOptions)
                ?? new AppConfiguration();
        }
        catch (Exception)
        {
            return new AppConfiguration();
        }
    }

    public async Task SaveAsync(AppConfiguration configuration)
    {
        await _writeLock.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(_configurationPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _configurationPath + ".tmp";

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions);
            }

            File.Move(temporaryPath, _configurationPath, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
