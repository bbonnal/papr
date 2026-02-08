using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using paprUI.Models;
using paprUI.ViewModels;
using paprUI.Views;

namespace paprUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serialService = new SerialService();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(serialService),
            };

            desktop.Exit += (s, e) => serialService.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}