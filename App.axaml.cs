using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VersionInfoMVVM.ViewModels;
using VersionInfoMVVM.Views;
using Avalonia.Controls;
using VersionInfoMVVM.Models;

namespace VersionInfoMVVM
{
    public partial class App : Application
    {
        public static MainWindow MainWindow; //HACK: сохранение окна
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dataUnit = new DataUnit();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(dataUnit),
                };
                MainWindow = (MainWindow)desktop.MainWindow; //HACK: сохранение окна
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
