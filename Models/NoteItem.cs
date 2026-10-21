using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DynamicIsland.Models;

public sealed class NoteItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private DateTime _updatedAt = DateTime.Now;
    private bool _isEditing;

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (_updatedAt == value) return;
            _updatedAt = value;
            OnPropertyChanged();
        }
    }

    // Selection is a transient view state, so it never reaches configuration.json.
    [JsonIgnore]
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
