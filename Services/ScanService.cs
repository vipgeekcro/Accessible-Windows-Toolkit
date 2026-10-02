using System.Diagnostics;
using AccessibleWindowsToolkit.Models;
using Microsoft.Win32;

namespace AccessibleWindowsToolkit.Services;

internal sealed record AppxPackage(string Name, string PackageFullName, bool NonRemovable);
internal sealed record ProvisionedPackage(string DisplayName, string PackageName);
internal sealed record AppxState(List<AppxPackage> Installed, List<ProvisionedPackage> Provisioned)
{
    internal bool Present => Installed.Count > 0 || Provisioned.Count > 0;
}

internal sealed class ScanService
{
    // Use a small line protocol instead of JSON. Windows PowerShell/cmdlets can
    // occasionally add extra serialized output around JSON, which makes a single
    // JsonDocument fragile. Package identifiers/full names do not contain '|'.
    private const string AppxScript = @"
$i=@(); $p=@();
try { $i=@(Get-AppxPackage -AllUsers -ErrorAction Stop | Select-Object Name,PackageFullName,NonRemovable) }
catch { $i=@(Get-AppxPackage -ErrorAction SilentlyContinue | Select-Object Name,PackageFullName,NonRemovable) }
try { $p=@(Get-AppxProvisionedPackage -Online -ErrorAction Stop | Select-Object DisplayName,PackageName) }
catch { $p=@() }
Write-Output 'ADSCAN|BEGIN'
foreach($x in $i) { Write-Output ('I|{0}|{1}|{2}' -f $x.Name,$x.PackageFullName,[bool]$x.NonRemovable) }
foreach($x in $p) { Write-Output ('P|{0}|{1}' -f $x.DisplayName,$x.PackageName) }
Write-Output 'ADSCAN|END'
";

    internal async Task<(List<AppxPackage> Installed, List<ProvisionedPackage> Provisioned)> Snapshot()
    {
        var result = await CommandRunner.PowerShell(AppxScript);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "AppX scan failed." : result.Output.Trim());

        var installed = new List<AppxPackage>();
        var provisioned = new List<ProvisionedPackage>();
        var began = false;
        var ended = false;

        foreach (var raw in result.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line == "ADSCAN|BEGIN") { began = true; continue; }
            if (line == "ADSCAN|END") { ended = true; continue; }
            if (!began || ended) continue;

            if (line.StartsWith("I|", StringComparison.Ordinal))
            {
                var parts = line.Split('|', 4);
                if (parts.Length == 4 && !string.IsNullOrWhiteSpace(parts[1]))
                    installed.Add(new AppxPackage(parts[1], parts[2], bool.TryParse(parts[3], out var nonRemovable) && nonRemovable));
            }
            else if (line.StartsWith("P|", StringComparison.Ordinal))
            {
                var parts = line.Split('|', 3);
                if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[1]))
                    provisioned.Add(new ProvisionedPackage(parts[1], parts[2]));
            }
        }

        if (!began || !ended)
            throw new InvalidOperationException("AppX scan returned an unexpected response and was not used.");

        return (installed, provisioned);
    }

    internal async Task<AppxState> Appx(string id)
    {
        var (installed, provisioned) = await Snapshot();
        return new(
            installed.Where(x => x.Name.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList(),
            provisioned.Where(x => x.DisplayName.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    internal async Task<bool> Winget(string id)
    {
        try
        {
            var result = await CommandRunner.Run("winget.exe", "list", "--id", id, "--exact", "--accept-source-agreements", "--disable-interactivity");
            return result.Output.Contains(id, StringComparison.OrdinalIgnoreCase);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    internal async Task<bool> OneDrive()
    {
        if (await Winget("Microsoft.OneDrive")) return true;
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe";
        if (Registry.CurrentUser.OpenSubKey(key) != null ||
            Registry.LocalMachine.OpenSubKey(key) != null ||
            Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe") != null)
            return true;
        return Process.GetProcessesByName("OneDrive").Length > 0;
    }

    internal async Task<List<Candidate>> Candidates()
    {
        var (installed, provisioned) = await Snapshot();
        var names = installed.Select(x => x.Name)
            .Concat(provisioned.Select(x => x.DisplayName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copilotPresent = await Winget("XP9CXNGPPJ97XX");
        var oneDrivePresent = await OneDrive();

        return CandidateCatalog.All.Where(c => c.Method switch
        {
            "Appx" or "TeamsNew" => names.Contains(c.Id),
            "WinGet" => copilotPresent,
            "OneDrive" => oneDrivePresent,
            _ => false
        }).ToList();
    }

    internal async Task<bool> Present(Candidate c) => c.Method switch
    {
        "Appx" or "TeamsNew" => (await Appx(c.Id)).Present,
        "WinGet" => await Winget(c.Id),
        "OneDrive" => await OneDrive(),
        _ => false
    };
}
