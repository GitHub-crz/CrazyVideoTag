using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace CrazyVideoTag.Controls;

/// <summary>
/// 支持虚拟化的换行面板：只生成当前视口内的项，几千个视频卡片一次放入也能流畅滚动。
/// 采用 pixel 滚动模式（ScrollViewer CanContentScroll=false），视口与滚动偏移从外层
/// ScrollViewer 读取，不依赖 IScrollInfo 桥接。
/// </summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel
{
    private static readonly DependencyProperty ItemIndexProperty = DependencyProperty.RegisterAttached(
        "ItemIndex", typeof(int), typeof(VirtualizingWrapPanel), new PropertyMetadata(-1));

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(268d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(258d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
    public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }

    private ScrollViewer? _scrollViewer;

    private ItemsControl? ItemsOwner => ItemsControl.GetItemsOwner(this);
    private int ItemCount => ItemsOwner?.Items.Count ?? 0;

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        // ScrollViewer 为 pixel 滚动，availableSize.Height 通常为 Infinity；
        // 视口大小与滚动偏移从外层 ScrollViewer 读取。
        var scrollViewer = GetScrollViewer();
        var viewportWidth = scrollViewer?.ViewportWidth ?? availableSize.Width;
        var viewportHeight = scrollViewer?.ViewportHeight ?? availableSize.Height;
        if (double.IsInfinity(viewportWidth) || viewportWidth <= 0)
        {
            viewportWidth = availableSize.Width;
        }

        if (double.IsInfinity(viewportWidth) || viewportWidth <= 0)
        {
            viewportWidth = ItemWidth;
        }

        if (double.IsInfinity(viewportHeight) || viewportHeight < 0)
        {
            viewportHeight = 0;
        }

        var itemCount = ItemCount;
        if (itemCount == 0)
        {
            RemoveAllChildren();
            return new WpfSize(viewportWidth, 0);
        }

        var itemsPerLine = Math.Max(1, (int)(viewportWidth / ItemWidth));
        var lineCount = (int)Math.Ceiling(itemCount / (double)itemsPerLine);
        var extentHeight = lineCount * ItemHeight;
        var offsetY = scrollViewer?.VerticalOffset ?? 0;

        var firstVisibleIndex = Math.Max(0, (int)(offsetY / ItemHeight) * itemsPerLine);
        var lastVisibleIndex = Math.Min(
            itemCount - 1,
            (int)Math.Ceiling((offsetY + viewportHeight) / ItemHeight) * itemsPerLine + itemsPerLine - 1);

        RealizeRange(firstVisibleIndex, lastVisibleIndex);

        // 返回完整内容高度，ScrollViewer 据此计算滚动范围。
        return new WpfSize(viewportWidth, extentHeight);
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        var scrollViewer = GetScrollViewer();
        var offsetY = scrollViewer?.VerticalOffset ?? 0;
        var itemsPerLine = Math.Max(1, (int)(finalSize.Width / ItemWidth));
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var index = (int)child.GetValue(ItemIndexProperty);
            if (index < 0)
            {
                continue;
            }

            var row = index / itemsPerLine;
            var column = index % itemsPerLine;
            child.Arrange(new WpfRect(column * ItemWidth, row * ItemHeight - offsetY, ItemWidth, ItemHeight));
        }

        return finalSize;
    }

    private ScrollViewer? GetScrollViewer()
    {
        if (_scrollViewer is not null && _scrollViewer.IsVisible)
        {
            return _scrollViewer;
        }

        _scrollViewer = FindScrollViewer(this);
        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        return _scrollViewer;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject current)
    {
        var parent = VisualTreeHelper.GetParent(current);
        while (parent is not null)
        {
            if (parent is ScrollViewer viewer)
            {
                return viewer;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0)
        {
            InvalidateMeasure();
        }
    }

    private void RealizeRange(int firstVisibleIndex, int lastVisibleIndex)
    {
        var generator = ItemContainerGenerator;

        var startPosition = generator.GeneratorPositionFromIndex(firstVisibleIndex);
        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (var i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                var child = generator.GenerateNext() as UIElement;
                if (child is null)
                {
                    continue;
                }

                child.SetValue(ItemIndexProperty, i);
                if (!Children.Contains(child))
                {
                    AddInternalChild(child);
                }

                generator.PrepareItemContainer(child);
                child.Measure(new WpfSize(ItemWidth, ItemHeight));
            }
        }

        // 移除已不在可见范围内的容器（保留 generator 的 realized 状态）。
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            var index = (int)Children[i].GetValue(ItemIndexProperty);
            if (index < firstVisibleIndex || index > lastVisibleIndex)
            {
                RemoveInternalChildRange(i, 1);
            }
        }
    }

    private void RemoveAllChildren()
    {
        if (Children.Count == 0)
        {
            return;
        }

        RemoveInternalChildRange(0, Children.Count);
    }
}
