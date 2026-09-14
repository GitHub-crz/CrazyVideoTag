using System.ComponentModel;

namespace CrazyVideoTag.Models;

public sealed class VideoPageItem : INotifyPropertyChanged
{
    private bool _isCurrent;

    public VideoPageItem(int pageNumber)
    {
        PageNumber = pageNumber;
    }

    public int PageNumber { get; }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value)
            {
                return;
            }

            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
