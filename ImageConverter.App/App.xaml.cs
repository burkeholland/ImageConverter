using System.IO;
using System.Windows;
using ImageConverter.Models;
using ImageConverter.Services;
using Wpf.Ui.Appearance;
using ImageFormat = ImageConverter.Models.ImageFormat;

namespace ImageConverter;

/// <summary>
/// Application entry point with command line handling.
/// </summary>
public partial class App : Application
{
    // Lazy-load conversion service to avoid startup cost when not needed
    private ImageConversionService? _conversionService;
    private ImageConversionService ConversionService => _conversionService ??= new ImageConversionService();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply system theme on background thread to avoid blocking startup
        Task.Run(() => ApplicationThemeManager.ApplySystemTheme());

        // Handle command line arguments
        if (e.Args.Length > 0)
        {
            var arg = e.Args[0].ToLowerInvariant();

            // --register: Register shell integration
            if (arg == "--register")
            {
                HandleRegistration();
                return;
            }

            // --unregister: Remove shell integration
            if (arg == "--unregister")
            {
                HandleUnregistration();
                return;
            }

            // --convert <format> <filepath>: Direct conversion from context menu
            if (arg == "--convert" && e.Args.Length >= 3)
            {
                HandleDirectConversion(e.Args[1], e.Args[2]);
                return;
            }

            // --custom <filepath>: Open main window with file loaded
            if (arg == "--custom" && e.Args.Length >= 2)
            {
                OpenMainWindowWithFile(e.Args[1]);
                return;
            }

            // Legacy: Just a file path (for backward compatibility)
            if (File.Exists(e.Args[0]) && ImageConversionService.IsSupportedFormat(e.Args[0]))
            {
                OpenMainWindowWithFile(e.Args[0]);
                return;
            }
        }

        // Normal startup with main window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    /// <summary>
    /// Handle direct format conversion from context menu.
    /// </summary>
    private async void HandleDirectConversion(string formatArg, string filePath)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!Enum.TryParse<ImageFormat>(formatArg, true, out var format))
        {
            MessageBox.Show($"Unknown format: {formatArg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        try
        {
            var options = new ConversionOptions
            {
                TargetFormat = format,
                Quality = format == ImageFormat.Jpeg || format == ImageFormat.WebP ? 85 : 100
            };

            var result = await ConversionService.ConvertAsync(filePath, options);

            if (!result.Success)
            {
                MessageBox.Show(
                    result.ErrorMessage ?? "Conversion failed",
                    "Conversion Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Shutdown();
    }

    /// <summary>
    /// Open the main window with a file pre-loaded.
    /// </summary>
    private void OpenMainWindowWithFile(string filePath)
    {
        var mainWindow = new MainWindow();
        mainWindow.LoadFile(filePath);
        MainWindow = mainWindow;
        mainWindow.Show();
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