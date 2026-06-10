using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CrazyVideoTag.Models;
using CrazyVideoTag.ViewModels;

namespace CrazyVideoTag;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private System.Windows.Point _tagDragStartPoint;
    private SelectableTagViewModel? _tagDragSource;
    private bool _tagDragOccurred;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
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
