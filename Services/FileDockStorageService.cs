using DynamicIsland.Models;
using System.IO;

namespace DynamicIsland.Services;

public sealed class FileDockStorageService
{
    private readonly string _storageRoot = Path.GetFullPath(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIsland",
        "FileDock"));

    public string StorageRoot => _storageRoot;

    public async Task<DockedFileItem?> StoreAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return null;
        }

        Directory.CreateDirectory(_storageRoot);
        var name = Path.GetFileName(sourcePath);
        var destination = GetUniqueDestination(name);

        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var target = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(target);

        return new DockedFileItem
        {
            Name = name,
            FullPath = destination,
            OriginalPath = sourcePath,
            Extension = Path.GetExtension(sourcePath),
            IsManagedCopy = true
        };
    }

    public bool IsManagedPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(_storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public string? GetDragOutPath(DockedFileItem item)
    {
        // The docked copy is already a real file in persistent local storage.
        // Hand its path to Windows so the drop target reads those bytes directly.
        return item.IsManagedCopy && IsManagedPath(item.FullPath) && File.Exists(item.FullPath)
            ? item.FullPath
            : null;
    }

    public void RemoveManagedCopy(DockedFileItem item)
    {
        if (!item.IsManagedCopy || !IsManagedPath(item.FullPath) || !File.Exists(item.FullPath))
        {
            return;
        }

        File.Delete(item.FullPath);
    }

    private string GetUniqueDestination(string name)
    {
        var destination = Path.Combine(_storageRoot, name);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var baseName = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            destination = Path.Combine(_storageRoot, $"{baseName} ({suffix}){extension}");
            if (!File.Exists(destination))
            {
                return destination;
            }
        }

        return Path.Combine(_storageRoot, $"{baseName}-{Guid.NewGuid():N}{extension}");
    }
}
