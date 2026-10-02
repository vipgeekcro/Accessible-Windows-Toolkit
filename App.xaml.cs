using System.Windows;

namespace AccessibleWindowsToolkit;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { await new ToolkitUi().Run(); }
        catch (Exception ex) { MessageBox.Show("Accessible Windows Toolkit could not start correctly.\n\n"+ex.Message,"Accessible Windows Toolkit - Startup Error",MessageBoxButton.OK,MessageBoxImage.Error); }
        finally { Shutdown(); }
    }
}
