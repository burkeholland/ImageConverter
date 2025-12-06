using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace ImageConverter.Services;

/// <summary>
/// Service for managing Windows shell context menu registration.
/// Creates a cascading submenu with format options.
/// </summary>
public class ShellIntegrationService
{
    private const string MenuName = "Convert Image";
    private const string RegistryKeyName = "ImageConverter";
    
    private static readonly string[] ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico"
    ];

    // Format options for the submenu
    private static readonly (string Name, string Arg)[] FormatOptions =
    [
        ("JPEG", "jpeg"),
        ("PNG", "png"),
        ("WebP", "webp"),
        ("GIF", "gif"),
        ("BMP", "bmp"),
        ("TIFF", "tiff"),
        ("ICO", "ico"),
    ];

    /// <summary>
    /// Check if the application is registered in the shell context menu.
    /// </summary>
    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey($"SystemFileAssociations\\.jpg\\shell\\{RegistryKeyName}");
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if the current process has administrator privileges.
    /// </summary>
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Register the application in the Windows shell context menu with cascading submenu.
    /// Requires administrator privileges.
    /// </summary>
    public static void Register(string executablePath)
    {
        if (!IsAdministrator())
        {
            throw new UnauthorizedAccessException("Administrator privileges required to register shell extension.");
        }

        foreach (var ext in ImageExtensions)
        {
            RegisterForExtension(ext, executablePath);
        }

        RegisterForSystemFileAssociation("image", executablePath);
    }

    /// <summary>
    /// Unregister the application from the Windows shell context menu.
    /// Requires administrator privileges.
    /// </summary>
    public static void Unregister()
    {
        if (!IsAdministrator())
        {
            throw new UnauthorizedAccessException("Administrator privileges required to unregister shell extension.");
        }

        foreach (var ext in ImageExtensions)
        {
            UnregisterForExtension(ext);
        }

        UnregisterForSystemFileAssociation("image");
    }

    /// <summary>
    /// Register cascading context menu for a specific file extension.
    /// </summary>
    private static void RegisterForExtension(string extension, string executablePath)
    {
        var keyPath = $"SystemFileAssociations\\{extension}\\shell\\{RegistryKeyName}";

        try
        {
            using var shellKey = Registry.ClassesRoot.CreateSubKey(keyPath);
            if (shellKey == null) return;

            shellKey.SetValue("", MenuName);
            shellKey.SetValue("Icon", $"\"{executablePath}\",0");
            shellKey.SetValue("Position", "Top");
            shellKey.SetValue("MUIVerb", MenuName);
            shellKey.SetValue("SubCommands", "");

            using var subShellKey = shellKey.CreateSubKey("shell");
            if (subShellKey == null) return;

            int order = 1;
            foreach (var (name, arg) in FormatOptions)
            {
                using var formatKey = subShellKey.CreateSubKey($"{order:D2}_{arg}");
                if (formatKey == null) continue;

                formatKey.SetValue("MUIVerb", $"Convert to {name}");

                using var commandKey = formatKey.CreateSubKey("command");
                commandKey?.SetValue("", $"\"{executablePath}\" --convert \"{arg}\" \"%1\"");
                
                order++;
            }

            // Add "Custom..." option at the end
            using var customKey = subShellKey.CreateSubKey($"{order:D2}_custom");
            if (customKey != null)
            {
                customKey.SetValue("MUIVerb", "Custom...");

                using var commandKey = customKey.CreateSubKey("command");
                commandKey?.SetValue("", $"\"{executablePath}\" --custom \"%1\"");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register for {extension}: {ex.Message}");
        }
    }

    /// <summary>
    /// Register for system file association (applies to all files of a type).
    /// </summary>
    private static void RegisterForSystemFileAssociation(string type, string executablePath)
    {
        var keyPath = $"SystemFileAssociations\\{type}\\shell\\{RegistryKeyName}";

        try
        {
            using var shellKey = Registry.ClassesRoot.CreateSubKey(keyPath);
            if (shellKey == null) return;

            shellKey.SetValue("", MenuName);
            shellKey.SetValue("Icon", $"\"{executablePath}\",0");
            shellKey.SetValue("Position", "Top");
            shellKey.SetValue("MUIVerb", MenuName);
            shellKey.SetValue("SubCommands", "");

            using var subShellKey = shellKey.CreateSubKey("shell");
            if (subShellKey == null) return;

            int order = 1;
            foreach (var (name, arg) in FormatOptions)
            {
                using var formatKey = subShellKey.CreateSubKey($"{order:D2}_{arg}");
                if (formatKey == null) continue;

                formatKey.SetValue("MUIVerb", $"Convert to {name}");

                using var commandKey = formatKey.CreateSubKey("command");
                commandKey?.SetValue("", $"\"{executablePath}\" --convert \"{arg}\" \"%1\"");
                
                order++;
            }

            using var customKey = subShellKey.CreateSubKey($"{order:D2}_custom");
            if (customKey != null)
            {
                customKey.SetValue("MUIVerb", "Custom...");

                using var commandKey = customKey.CreateSubKey("command");
                commandKey?.SetValue("", $"\"{executablePath}\" --custom \"%1\"");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register for {type}: {ex.Message}");
        }
    }

    /// <summary>
    /// Unregister context menu for a specific file extension.
    /// </summary>
    private static void UnregisterForExtension(string extension)
    {
        var keyPath = $"SystemFileAssociations\\{extension}\\shell\\{RegistryKeyName}";

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister for {extension}: {ex.Message}");
        }
    }

    /// <summary>
    /// Unregister from system file association.
    /// </summary>
    private static void UnregisterForSystemFileAssociation(string type)
    {
        var keyPath = $"SystemFileAssociations\\{type}\\shell\\{RegistryKeyName}";

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to unregister for {type}: {ex.Message}");
        }
    }

    /// <summary>
    /// Restart as administrator to perform registration.
    /// </summary>
    public static bool RestartAsAdministrator(string arguments = "")
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
