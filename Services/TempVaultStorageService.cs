using System.IO;

namespace DynamicIsland.Services;

// TempVault watches its Inbox and moves stable files into managed storage.
// Never pass it the island's only dock copy: export a separate copy instead.
public sealed class TempVaultStorageService
{
    public static bool IsAvailable(string root) =>
        !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root) &&
        Directory.Exists(Path.Combine(root, "Inbox"));

    public async Task<string> CopyToInboxAsync(string sourcePath, string root)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The docked copy is missing.", sourcePath);
        if (!IsAvailable(root))
            throw new DirectoryNotFoundException("TempVault Inbox was not found. Check its storage root in Settings.");

        var inbox = Path.Combine(Path.GetFullPath(root), "Inbox");
        var name = Path.GetFileName(sourcePath);
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        var destination = Path.Combine(inbox, name);
        for (var suffix = 2; File.Exists(destination); suffix++)
            destination = Path.Combine(inbox, $"{stem} ({suffix}){extension}");

        // TempVault ignores .partial files. A complete atomic rename is what its
        // watcher sees, so large exports are never ingested half-written.
        var pending = Path.Combine(inbox, $".{Guid.NewGuid():N}.partial");
        try
        {
            await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var target = new FileStream(pending, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
                await source.CopyToAsync(target);

            File.Move(pending, destination);
            return destination;
        }
        finally
        {
            if (File.Exists(pending)) File.Delete(pending);
        }
    }
}
