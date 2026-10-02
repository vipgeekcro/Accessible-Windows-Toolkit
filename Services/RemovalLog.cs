using System.IO;
namespace AccessibleWindowsToolkit.Services;

internal sealed class RemovalLog
{
    private readonly string path;
    internal RemovalLog()
    {
        var folder = AppContext.BaseDirectory;
        var old = Directory.GetFiles(folder,"AccessibleWindowsToolkit-removal-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc).Skip(9).ToArray();
        foreach (var file in old) { try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        path = Path.Combine(folder,$"AccessibleWindowsToolkit-removal-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
        File.WriteAllText(path,"Accessible Windows Toolkit removal log"+Environment.NewLine);
    }
    internal void Write(string text) => File.AppendAllText(path,$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text}{Environment.NewLine}");
}
