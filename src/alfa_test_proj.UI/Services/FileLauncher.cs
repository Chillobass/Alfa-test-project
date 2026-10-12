using System.Diagnostics;
using System.Runtime.InteropServices;

namespace alfa_test_proj.UI.Services;

public static class FileLauncher
{
    public static void OpenFile(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public static void ShowInFolder(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
            return;
        }

        var directory = Path.GetDirectoryName(path) ?? path;
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }
}
