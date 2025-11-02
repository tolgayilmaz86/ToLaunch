using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ToLaunch.Services;
public class IconService
{
    private readonly string _iconCacheDirectory;

    public IconService()
    {
        _iconCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ToLaunch", "Icons");

        if (!Directory.Exists(_iconCacheDirectory))
        {
            Directory.CreateDirectory(_iconCacheDirectory);
        }
    }

    public async Task<string?> ExtractIconAsync(string executablePath)
    {
        if (!File.Exists(executablePath))
            return null;

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(executablePath);
            var hash = GetFileHash(executablePath);
            var iconPath = Path.Combine(_iconCacheDirectory, $"{fileName}_{hash}.png");

            // Check if icon already cached
            if (File.Exists(iconPath))
                return iconPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await Task.Run(() => ExtractIconWindows(executablePath, iconPath));
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract icon: {ex.Message}");
            return null;
        }
    }

    private static string? ExtractIconWindows(string executablePath, string outputPath)
    {
        try
        {
            IntPtr[] largeIcons = new IntPtr[1];
            IntPtr[] smallIcons = new IntPtr[1];

            // Extract both large and small icons
            int iconCount = ExtractIconEx(executablePath, 0, largeIcons, smallIcons, 1);

            if (iconCount == 0)
                return null;

            // Prefer large icon, fallback to small icon
            IntPtr hIcon = largeIcons[0] != IntPtr.Zero ? largeIcons[0] : smallIcons[0];

            if (hIcon == IntPtr.Zero)
                return null;

            // Convert icon to bitmap and save as PNG
            using (var icon = System.Drawing.Icon.FromHandle(hIcon))
            using (var bitmap = icon.ToBitmap())
            {
                bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            // Clean up icon handles
            if (largeIcons[0] != IntPtr.Zero) DestroyIcon(largeIcons[0]);
            if (smallIcons[0] != IntPtr.Zero) DestroyIcon(smallIcons[0]);

            return outputPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract icon from executable: {ex.Message}");
            return null;
        }
    }

    private static string GetFileHash(string filePath)
    {
        // Simple hash based on file path and last write time
        var info = new FileInfo(filePath);
        return $"{info.LastWriteTimeUtc.Ticks:X}";
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex,
        IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}