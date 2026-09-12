using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrazyVideoTag.Models;

public sealed class PositionPreviewItem : INotifyPropertyChanged
{
    private string? _imagePath;
    private string? _status = "等待生成";
    private bool _isCover;

    public PositionPreviewItem(int percentage)
    {
        Percentage = percentage;
    }

    public int Percentage { get; }

    public string Caption => $"{Percentage}%";

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            if (_imagePath == value)
            {
                return;
            }

            _imagePath = value;
            OnPropertyChanged();
        }
    }

    public string? Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public bool IsCover
    {
        get => _isCover;
        set
        {
            if (_isCover == value)
            {
                return;
            }

            _isCover = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
