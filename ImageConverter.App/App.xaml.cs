using System.IO;
using System.Windows;
using ImageConverter.Services;
using ImageConverter.Views;
using Wpf.Ui.Appearance;

namespace ImageConverter;

/// <summary>
/// Application entry point with command line handling.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply system theme and accent color
        ApplicationThemeManager.ApplySystemTheme();

        // Handle command line arguments for shell integration
        if (e.Args.Length > 0)
        {
            var arg = e.Args[0].ToLowerInvariant();

            if (arg == "--register")
            {
                HandleRegistration();
                return;
            }

            if (arg == "--unregister")
            {
                HandleUnregistration();
                return;
            }

            // Check if it's a file path for quick conversion
            if (File.Exists(e.Args[0]) && ImageConversionService.IsSupportedFormat(e.Args[0]))
            {
                // Show quick convert window for context menu invocation
                var quickWindow = new QuickConvertWindow(e.Args[0]);
                quickWindow.ShowDialog();

                if (quickWindow.OpenMainWindow)
                {
                    // User wants more options, open main window with file loaded
                    var mainWindow = new MainWindow();
                    mainWindow.LoadFile(e.Args[0]);
                    MainWindow = mainWindow;
                    mainWindow.Show();
                    return;
                }
                
                Shutdown();
                return;
            }
        }

        // Normal startup with main window
        var mainWindow2 = new MainWindow();
        mainWindow2.Show();
    }

    private void HandleRegistration()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? "";
            ShellIntegrationService.Register(exePath);
            MessageBox.Show(
                "Image Converter has been added to the right-click menu!",
                "Registration Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to register: {ex.Message}",
                "Registration Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Shutdown();
        }
    }

    private void HandleUnregistration()
    {
        try
        {
            ShellIntegrationService.Unregister();
            MessageBox.Show(
                "Image Converter has been removed from the right-click menu.",
                "Unregistration Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to unregister: {ex.Message}",
                "Unregistration Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Shutdown();
        }
    }
}