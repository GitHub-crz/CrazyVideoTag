using System.Windows;
using CrazyVideoTag.Services;

namespace CrazyVideoTag;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        base.OnStartup(e);
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            WindowThemeHelper.ApplyDarkTheme(window);
        }
    }
}
