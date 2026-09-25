using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DynamicIsland.Services;

public sealed class ClipboardService
{
    public void SetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    public async Task<string> GetTextAsync()
    {
        var content = Clipboard.GetContent();
        return content.Contains(StandardDataFormats.Text)
            ? await content.GetTextAsync()
            : string.Empty;
    }

    public async Task<IReadOnlyList<StorageFile>> GetFilesAsync()
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.StorageItems))
        {
            return [];
        }

        var items = await content.GetStorageItemsAsync();
        return items.OfType<StorageFile>().ToList();
    }

    public async Task SetFileAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var package = new DataPackage();
        package.SetStorageItems(new[] { file });
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }
}
