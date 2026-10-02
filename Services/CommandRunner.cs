using System.Diagnostics;
using System.Text;

namespace AccessibleWindowsToolkit.Services;

internal sealed record CommandResult(int ExitCode, string Output);
internal static class CommandRunner
{
    internal static async Task<CommandResult> PowerShell(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script));
        return await Run("powershell.exe", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded);
    }
    internal static async Task<CommandResult> Run(string exe, params string[] args)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {exe}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, (await output) + (await error));
    }
    internal static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
}
