using System.Windows;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.UndoRedo;
using Microsoft.Extensions.DependencyInjection;

namespace InkSlate.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "InkSlate Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        TryRegisterFileAssociation();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        if (InkSlate.Core.Services.StartupArgumentResolver.TryResolveNotexFilePath(e.Args, out var targetPath) &&
            !string.IsNullOrWhiteSpace(targetPath))
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            _ = mainVm.LoadFromFileAsync(targetPath);
        }
    }

    private static void TryRegisterFileAssociation()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath) &&
                string.Equals(System.IO.Path.GetExtension(exePath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                InkSlate.Core.Services.FileAssociationService.RegisterCurrentUserAssociation(exePath);
            }
        }
        catch
        {
            // Best-effort: silent fallback if registry writes are prohibited
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var mainViewModel = _serviceProvider?.GetService<MainViewModel>();
            if (mainViewModel is not null && !mainViewModel.IsDirty)
            {
                mainViewModel.DeleteCurrentRecoveryFile();
            }
        }
        catch
        {
            // Do not fail exit
        }

        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<NoteDocument>();
        services.AddSingleton<UndoRedoManager>();
        services.AddSingleton<ToolbarViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
