using System.ComponentModel;
using System.Runtime.CompilerServices;
using CrazyVideoTag.Models;

namespace CrazyVideoTag.ViewModels;

public sealed class SelectableTagViewModel : INotifyPropertyChanged
{
    private bool _isChecked;

    public required TagDefinition Definition { get; init; }
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Color => Definition.Color;
    public TagKind Kind => Definition.Kind;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Color));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
