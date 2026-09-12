using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CrazyVideoTag.Models;
using CrazyVideoTag.ViewModels;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ToolTip = System.Windows.Controls.ToolTip;

namespace CrazyVideoTag;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private System.Windows.Point _tagDragStartPoint;
    private SelectableTagViewModel? _tagDragSource;
    private bool _tagDragOccurred;
    private MenuItem? _previewToolTipItem;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.DisplayRefreshed += ScrollVideosToTop;
        _viewModel.PositionPreviewsStarted += ScrollPositionPreviewsToTop;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private void ScrollPositionPreviewsToTop()
    {
        Dispatcher.BeginInvoke(new Action(() => PositionPreviewScroller.ScrollToTop()), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ScrollVideosToTop()
    {
        Dispatcher.BeginInvoke(new Action(() => MainVideoScroller.ScrollToTop()), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNode node)
        {
            _viewModel.SelectFolder(node);
        }
    }

    private void VideoCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VideoItem video)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _viewModel.ToggleVideoSelection(video);
        }
        else
        {
            _viewModel.SelectSingleVideo(video);
        }

        if (e.ClickCount == 2 && _viewModel.OpenSelectedVideoCommand.CanExecute(null))
        {
            _viewModel.OpenSelectedVideoCommand.Execute(null);
        }
    }

    private void VideoCard_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is VideoItem video)
        {
            _viewModel.SelectedVideo = video;
        }
    }

    private void PositionPreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PositionPreviewItem item } && item.ImagePath is not null)
        {
            _viewModel.SetCoverFromPreviewItem(item);
            e.Handled = true;
        }
    }

    private void PercentageMenuItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not MenuItem item
            || item.CommandParameter is not string percentText
            || !double.TryParse(percentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percentage))
        {
            return;
        }

        ClosePreviewToolTip();
        _previewToolTipItem = item;

        var tooltip = BuildPreviewToolTip();
        item.ToolTip = tooltip;
        tooltip.IsOpen = true;
        _viewModel.PreviewThumbnailAt(percentage);
    }

    private void PercentageMenuItem_MouseLeave(object sender, MouseEventArgs e)
    {
        ClosePreviewToolTip();
    }

    private void PreviewContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        ClosePreviewToolTip();
    }

    private void ClosePreviewToolTip()
    {
        if (_previewToolTipItem is null)
        {
            return;
        }

        if (_previewToolTipItem.ToolTip is ToolTip tooltip)
        {
            tooltip.IsOpen = false;
        }
        _previewToolTipItem.ToolTip = null;
        _previewToolTipItem = null;
    }

    private System.Windows.Controls.ToolTip BuildPreviewToolTip()
    {
        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("TextMuted"),
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 216
        };
        statusText.SetBinding(TextBlock.TextProperty, new Binding(nameof(MainViewModel.PreviewStatus)) { Source = _viewModel });

        var image = new System.Windows.Controls.Image
        {
            Width = 216,
            Height = 135,
            Stretch = Stretch.UniformToFill
        };
        image.SetBinding(System.Windows.Controls.Image.SourceProperty, new Binding(nameof(MainViewModel.PreviewImageSource)) { Source = _viewModel });
        image.Clip = new RectangleGeometry(new Rect(0, 0, 216, 135), 6, 6);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x0E, 0x12)),
            Width = 216,
            Height = 135,
            Child = image
        };

        var panel = new StackPanel { Margin = new Thickness(10), Children = { statusText, border } };

        return new ToolTip
        {
            Content = panel,
            Placement = PlacementMode.Right,
            VerticalOffset = -24,
            Background = (Brush)Application.Current.FindResource("PanelBg"),
            BorderBrush = (Brush)Application.Current.FindResource("BorderStrong"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            HasDropShadow = true,
            StaysOpen = true
        };
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _viewModel.DeleteSelectedVideoCommand.CanExecute(null))
        {
            _viewModel.DeleteSelectedVideoCommand.Execute(null);
        }
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not FrameworkElement element)
        {
            return;
        }

        element.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        SearchBox.Focus();
    }

    private void VideoScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.VerticalChange <= 0)
        {
            return;
        }

        if (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < 800 && _viewModel.LoadMoreVideosCommand.CanExecute(null))
        {
            _viewModel.LoadMoreVideosCommand.Execute(null);
        }
    }

    private void TagRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _tagDragStartPoint = e.GetPosition(this);
        _tagDragSource = (sender as FrameworkElement)?.DataContext as SelectableTagViewModel;
        _tagDragOccurred = false;
    }

    private void TagRow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _tagDragSource is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _tagDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _tagDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _tagDragOccurred = true;
        DragDrop.DoDragDrop((DependencyObject)sender, _tagDragSource, System.Windows.DragDropEffects.Move);
        _tagDragSource = null;
    }

    private void TagRow_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SelectableTagViewModel)) is SelectableTagViewModel source
            && (sender as FrameworkElement)?.DataContext is SelectableTagViewModel target
            && source.Kind == target.Kind)
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TagRow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SelectableTagViewModel)) is SelectableTagViewModel source
            && (sender as FrameworkElement)?.DataContext is SelectableTagViewModel target)
        {
            _viewModel.MoveTag(source, target);
        }

        e.Handled = true;
    }

    private void TagRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_tagDragOccurred)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is not SelectableTagViewModel row)
        {
            return;
        }

        if (e.ClickCount == 2 && _viewModel.EditTagCommand.CanExecute(row))
        {
            _viewModel.EditTagCommand.Execute(row);
            e.Handled = true;
            return;
        }

        row.IsChecked = !row.IsChecked;
    }
}
