namespace DynamicIsland.Models;

using System.Text.Json.Serialization;

public sealed class DockedFileItem
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public string Extension { get; init; } = string.Empty;

    [JsonIgnore]
    public string TypeLabel
    {
        get
        {
            var extension = Extension.TrimStart('.').ToUpperInvariant();
            return extension switch
            {
                "DOCX" => "DOC",
                "XLSX" => "XLS",
                "PPTX" => "PPT",
                "JPEG" => "JPG",
                "" => "FILE",
                _ when extension.Length <= 4 => extension,
                _ => extension[..4]
            };
        }
    }

    public string OriginalPath { get; init; } = string.Empty;

    public bool IsManagedCopy { get; init; }
}
