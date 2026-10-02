using System.IO;
using System.Diagnostics;
using AccessibleWindowsToolkit.Models;

namespace AccessibleWindowsToolkit.Services;

internal sealed record RemovalResult(Candidate Candidate, string Status, string Message);
internal sealed class RemovalService(ScanService scan)
{
    private static RemovalResult Result(Candidate c, string status, string message) => new(c,status,message);
    private static RemovalResult Absent(Candidate c) => Result(c,"NotInstalled","Already absent.");
    private static RemovalResult Success(Candidate c) => Result(c,"Removed","Successfully removed and verified.");
    private static RemovalResult Failed(Candidate c,string message) => Result(c,"Error",message);
    private async Task<RemovalResult> Appx(Candidate c,RemovalLog log)
    {
        var state = await scan.Appx(c.Id);
        log.Write($"PRECHECK | installed={state.Installed.Count} provisioned={state.Provisioned.Count} present={state.Present}");
        if (!state.Present) return Absent(c);
        if (state.Installed.Any(x=>x.NonRemovable)) return Failed(c,"Windows reports this package as NonRemovable. No removal command was executed.");
        var errors = new List<string>();
        var notes = new List<string>();
        foreach(var package in state.Installed)
            try { log.Write($"ACTION | Remove-AppxPackage -AllUsers | {package.PackageFullName}"); var r=await CommandRunner.PowerShell($"Remove-AppxPackage -Package {CommandRunner.Quote(package.PackageFullName)} -AllUsers -ErrorAction Stop");if(r.ExitCode!=0) throw new InvalidOperationException(r.Output);log.Write("COMMAND | Remove-AppxPackage -AllUsers completed"); }
            catch(Exception e){errors.Add(e.Message);log.Write("ERROR | Remove-AppxPackage -AllUsers | "+e.Message);}
        foreach(var package in state.Provisioned)
            try { log.Write($"ACTION | Remove-AppxProvisionedPackage | {package.PackageName}");var r=await CommandRunner.PowerShell($"Remove-AppxProvisionedPackage -Online -PackageName {CommandRunner.Quote(package.PackageName)} -AllUsers -ErrorAction Stop | Out-Null");if(r.ExitCode!=0)throw new InvalidOperationException(r.Output);log.Write("COMMAND | provisioned removal completed"); }
            catch(Exception e){notes.Add(e.Message);log.Write("COMMAND NOTE | provisioned removal returned: "+e.Message);}
        var after=await scan.Appx(c.Id);log.Write($"VERIFY | installed={after.Installed.Count} provisioned={after.Provisioned.Count} present={after.Present}");
        if(!after.Present)
        {
            if(notes.Count>0)log.Write("INFO | A provisioned-package command reported an issue, but verification confirms the package is absent.");
            return Success(c);
        }
        var allMessages=errors.Concat(notes).ToList();
        return Failed(c,allMessages.Count>0?string.Join(" | ",allMessages):"Package is still detected after removal attempt.");
    }
    private async Task<RemovalResult> TeamsNew(Candidate c,RemovalLog log)
    {
        var state=await scan.Appx("MSTeams");
        log.Write($"PRECHECK | installed={state.Installed.Count} provisioned={state.Provisioned.Count} present={state.Present}");
        if(!state.Present)return Absent(c);
        var errors=new List<string>();
        // The reference's New Teams path performs its own all-users removal, then provisioning removal.
        foreach(var package in state.Installed)
            try{log.Write($"ACTION | New Teams Remove-AppxPackage -AllUsers | {package.PackageFullName}");var r=await CommandRunner.PowerShell($"Remove-AppxPackage -Package {CommandRunner.Quote(package.PackageFullName)} -AllUsers -ErrorAction Stop");if(r.ExitCode!=0)throw new InvalidOperationException(r.Output);log.Write("COMMAND | New Teams all-users package removal completed");}
            catch(Exception e){errors.Add(e.Message);log.Write("ERROR | New Teams all-users removal | "+e.Message);break;}
        foreach(var package in state.Provisioned)
            try{log.Write($"ACTION | New Teams Remove-AppxProvisionedPackage | {package.PackageName}");var r=await CommandRunner.PowerShell($"Remove-AppxProvisionedPackage -Online -PackageName {CommandRunner.Quote(package.PackageName)} -AllUsers -ErrorAction Stop | Out-Null");if(r.ExitCode!=0)throw new InvalidOperationException(r.Output);log.Write("COMMAND | New Teams provisioned removal completed");}
            catch(Exception e){log.Write("COMMAND NOTE | New Teams provisioned removal returned: "+e.Message);}
        await Task.Delay(500);
        var after=await scan.Appx("MSTeams");log.Write($"VERIFY | installed={after.Installed.Count} provisioned={after.Provisioned.Count} present={after.Present}");
        return after.Present?Failed(c,errors.Count>0?string.Join(" | ",errors):"New Teams is still detected after the documented all-users removal attempt."):Success(c);
    }
    private async Task<RemovalResult> Winget(Candidate c,RemovalLog log)
    {
        if(!await scan.Winget(c.Id))return Absent(c);
        int exit=-1;
        try{log.Write($"ACTION | winget uninstall --id {c.Id} --exact --silent --disable-interactivity");var r=await CommandRunner.Run("winget.exe","uninstall","--id",c.Id,"--exact","--silent","--accept-source-agreements","--disable-interactivity");exit=r.ExitCode;log.Write($"COMMAND | exitCode={exit} | output={r.Output.Replace('\r',' ').Replace('\n',' ')}");}
        catch(Exception e){log.Write("ERROR | "+e.Message);}
        var present=await scan.Winget(c.Id);log.Write($"VERIFY | present={present}");
        return present?Failed(c,$"winget exit code {exit}; application is still detected."):Success(c);
    }
    private async Task<RemovalResult> OneDrive(Candidate c,RemovalLog log)
    {
        if(!await scan.OneDrive())return Absent(c);
        foreach(var p in Process.GetProcessesByName("OneDrive"))try{p.Kill();}catch(Exception e){log.Write("COMMAND NOTE | stop OneDrive | "+e.Message);} 
        log.Write("ACTION | stopped OneDrive process if running");
        var errors=new List<string>();
        if(await scan.Winget("Microsoft.OneDrive"))
            try{var r=await CommandRunner.Run("winget.exe","uninstall","--id","Microsoft.OneDrive","--exact","--silent","--accept-source-agreements","--disable-interactivity");log.Write($"COMMAND | winget exitCode={r.ExitCode} | {r.Output.Replace('\r',' ').Replace('\n',' ')}");}
            catch(Exception e){errors.Add(e.Message);log.Write("ERROR | "+e.Message);}
        if(await scan.OneDrive())
        {
            var setup=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","OneDriveSetup.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"SysWOW64","OneDriveSetup.exe")}.FirstOrDefault(File.Exists);
            if(setup!=null)try{var r=await CommandRunner.Run(setup,"/uninstall");log.Write($"COMMAND | OneDriveSetup /uninstall exitCode={r.ExitCode}");if(r.ExitCode!=0)errors.Add($"OneDriveSetup exit code {r.ExitCode}");}
                catch(Exception e){errors.Add(e.Message);log.Write("ERROR | "+e.Message);}
        }
        await Task.Delay(700);var present=await scan.OneDrive();log.Write($"VERIFY | present={present}");
        return present?Failed(c,errors.Count>0?string.Join(" | ",errors):"OneDrive is still detected."):Success(c);
    }
    internal async Task<List<RemovalResult>> Remove(IReadOnlyList<Candidate> selected,Action<int,int,string> progress)
    {
        var log=new RemovalLog();log.Write($"Removal session started. Selected={selected.Count}");var results=new List<RemovalResult>();
        for(int i=0;i<selected.Count;i++)
        {
            var c=selected[i];progress(i+1,selected.Count,c.DisplayName);log.Write($"BEGIN | {c.DisplayName} | Id={c.Id} | Method={c.Method}");
            RemovalResult result;
            try {result=c.Method switch {"Appx"=>await Appx(c,log),"TeamsNew"=>await TeamsNew(c,log),"WinGet"=>await Winget(c,log),"OneDrive"=>await OneDrive(c,log),_=>Failed(c,"Unknown removal method.")};}
            catch(Exception e){log.Write($"UNHANDLED ITEM ERROR | Id={c.Id} | {e.Message}");result=Failed(c,e.Message);}
            results.Add(result);log.Write($"RESULT | {result.Status} | {result.Message}");
        }
        log.Write($"Removal session finished. Success={results.Count(x=>x.Status=="Removed")} Failed={results.Count(x=>x.Status=="Error")} AlreadyAbsent={results.Count(x=>x.Status=="NotInstalled")}");
        return results;
    }
}
