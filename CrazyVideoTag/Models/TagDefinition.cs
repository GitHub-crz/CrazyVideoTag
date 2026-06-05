using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrazyVideoTag.Models;

public sealed class TagDefinition : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _color = "#4F8EF7";
    private TagKind _kind;
    private int _sortOrder;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Color
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    public TagKind Kind
    {
        get => _kind;
        set => SetField(ref _kind, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetField(ref _sortOrder, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
