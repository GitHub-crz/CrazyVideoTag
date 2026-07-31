using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfSize = System.Windows.Size;

namespace CrazyVideoTag.Controls;

/// <summary>
/// 支持虚拟化的换行面板：只生成当前视口内的项，几千个视频卡片一次放入也能流畅滚动。
/// 需要与 ScrollViewer 配合（CanContentScroll=true，默认即为 true）。
/// </summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
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

    private WpfSize _viewportSize;
    private WpfSize _extentSize;
    private WpfPoint _offset;

    public ScrollViewer? ScrollOwner { get; set; }

    private ItemsControl? ItemsOwner => ItemsControl.GetItemsOwner(this);
    private int ItemCount => ItemsOwner?.Items.Count ?? 0;
    private int ItemsPerLine => (int)Math.Max(1, Math.Floor((_viewportSize.Width + 0.5) / ItemWidth));

    public bool CanVerticallyScroll { get; set; }
    public bool CanHorizontallyScroll { get; set; }

    public double ExtentHeight => _extentSize.Height;
    public double ExtentWidth => _extentSize.Width;
    public double ViewportHeight => _viewportSize.Height;
    public double ViewportWidth => _viewportSize.Width;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;

    public void LineUp() => SetVerticalOffset(VerticalOffset - ItemHeight);
    public void LineDown() => SetVerticalOffset(VerticalOffset + ItemHeight);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelUp() => LineUp();
    public void MouseWheelDown() => LineDown();
    public void MouseWheelLeft() => SetHorizontalOffset(HorizontalOffset - ItemHeight);
    public void MouseWheelRight() => SetHorizontalOffset(HorizontalOffset + ItemHeight);

    public void SetHorizontalOffset(double offset)
    {
        var clamped = Math.Clamp(offset, 0, Math.Max(0, ExtentWidth - ViewportWidth));
        if (Math.Abs(clamped - _offset.X) < 0.1)
        {
            return;
        }

        _offset.X = clamped;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public void SetVerticalOffset(double offset)
    {
        var clamped = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (Math.Abs(clamped - _offset.Y) < 0.1)
        {
            return;
        }

        _offset.Y = clamped;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public WpfRect MakeVisible(Visual visual, WpfRect rectangle)
    {
        if (rectangle.IsEmpty || visual is null || visual == this || !IsAncestorOf(visual))
        {
            return WpfRect.Empty;
        }

        rectangle = visual.TransformToAncestor(this).TransformBounds(rectangle);
        rectangle.Offset(_offset.X, _offset.Y);

        var x = Math.Round(rectangle.Left - 0.5);
        var y = Math.Round(rectangle.Top - 0.5);
        SetHorizontalOffset(x);
        SetVerticalOffset(y);

        rectangle.X += _offset.X - x;
        rectangle.Y += _offset.Y - y;
        return rectangle;
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        // ScrollViewer 首次测量可能传 PositiveInfinity，此时不能返回 Infinity 的 DesiredSize。
        if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
        {
            RemoveAllChildren();
            return new WpfSize(0, 0);
        }

        var itemCount = ItemCount;
        if (itemCount == 0 || availableSize.Width <= 0)
        {
            RemoveAllChildren();
            return new WpfSize(0, 0);
        }

        _viewportSize = availableSize;
        var itemsPerLine = ItemsPerLine;
        var lineCount = (int)Math.Ceiling(itemCount / (double)itemsPerLine);
        _extentSize = new WpfSize(availableSize.Width, lineCount * ItemHeight);

        var firstVisibleIndex = Math.Max(0, (int)(_offset.Y / ItemHeight) * itemsPerLine);
        var lastVisibleIndex = Math.Min(
            itemCount - 1,
            (int)Math.Ceiling((_offset.Y + availableSize.Height) / ItemHeight) * itemsPerLine + itemsPerLine - 1);

        RealizeRange(firstVisibleIndex, lastVisibleIndex);
        return availableSize;
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        var itemsPerLine = ItemsPerLine;
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
            child.Arrange(new WpfRect(column * ItemWidth, row * ItemHeight - _offset.Y, ItemWidth, ItemHeight));
        }

        return finalSize;
    }

    private void RealizeRange(int firstVisibleIndex, int lastVisibleIndex)
    {
        RemoveAllChildren();
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
                AddInternalChild(child);
                generator.PrepareItemContainer(child);
                child.Measure(new WpfSize(ItemWidth, ItemHeight));
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
