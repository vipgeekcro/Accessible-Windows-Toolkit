using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using AccessibleWindowsToolkit.Models;
using AccessibleWindowsToolkit.Services;

namespace AccessibleWindowsToolkit;

internal sealed class ToolkitUi
{
    private readonly ScanService scan = new();
    private readonly List<CheckBox> boxes = new();
    private readonly Dictionary<string,Candidate> byId = CandidateCatalog.All.ToDictionary(x=>x.Id,StringComparer.OrdinalIgnoreCase);
    private static readonly (string Id,string Name)[] Categories = {("Microsoft","Microsoft Apps and Components"),("Xbox","Xbox and Gaming"),("Widgets","Widgets"),("ThirdParty","Third-party Software"),("OEM","OEM Software")};
    private Window main = null!;
    private StackPanel panel = null!;
    private Button selectAll = null!, deselectAll = null!, debloat = null!;
    private static Window Load(string name)
    {
        var resource = Application.GetResourceStream(new Uri($"Views/{name}.xaml", UriKind.Relative))
            ?? throw new InvalidOperationException($"Embedded UI resource '{name}.xaml' was not found.");
        using var stream = resource.Stream;
        return (Window)XamlReader.Load(stream);
    }
    private static T Find<T>(Window window,string name) where T:FrameworkElement => (T)window.FindName(name);
    private static void Shortcut(Window window, params (Key Key,Button Button)[] bindings)
    {
        window.PreviewKeyDown += (_,e) =>
        {
            if((Keyboard.Modifiers & ModifierKeys.Alt)==0)return;
            foreach(var (key,button) in bindings)
                if(e.SystemKey==key){if(button.IsEnabled)button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;break;}
        };
    }
    private static async Task<T> Busy<T>(string view, Window? owner, Func<Action<int,int,string>?,Task<T>> work)
    {
        var loading=Load(view);if(owner!=null)loading.Owner=owner;
        Exception? failure=null;T value=default!;
        loading.ContentRendered += async (_,_) =>
        {
            await Task.Delay(view=="LoadingWindow"?200:250);
            try
            {
                Action<int,int,string>? progress=view=="RemovalWindow"?(i,total,name)=>
                {
                    // Removal runs on a worker thread. All WPF control access must
                    // be marshalled back to the window dispatcher.
                    loading.Dispatcher.Invoke(() =>
                    {
                        var status=Find<TextBlock>(loading,"RemovalStatusText");
                        status.Text=$"Uninstalling {name}...";
                        Find<TextBlock>(loading,"RemovalDetailText").Text=$"Item {i} of {total}";
                        var peer=UIElementAutomationPeer.CreatePeerForElement(status) ?? new TextBlockAutomationPeer(status);
                        peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                    },DispatcherPriority.Render);
                }:null;
                value=await Task.Run(()=>work(progress));
            }
            catch(Exception ex){failure=ex;}
            finally{loading.Close();}
        };
        loading.ShowDialog();
        if(failure!=null)throw failure;
        return value;
    }
    internal async Task Run()
    {
        var warning=Load("WarningWindow");
        Find<TextBlock>(warning,"WarningText").Text="""
Accessible Windows Toolkit allows you to remove optional Windows applications, components, and preinstalled software from your computer.

Some items offered by this program provide specific Windows features or functionality. Removing them may cause those features, applications, or related functionality to become unavailable.

Before removing an item, make sure you understand what it does and that you no longer need its functionality.

Accessible Windows Toolkit does not automatically remove anything. You decide what to remove, and only the items you explicitly select will be processed.

Although Accessible Windows Toolkit is designed to offer components that can be removed without intentionally damaging the core Windows operating system, Windows configurations and installed software can vary. Removing a component may have unintended effects on features or applications that depend on it.

You use Accessible Windows Toolkit and remove components at your own responsibility. Always review your selections carefully before proceeding.
""";
        var proceed=Find<Button>(warning,"ContinueButton");var exit=Find<Button>(warning,"ExitButton");
        proceed.Click+=(_,_)=>warning.DialogResult=true;exit.Click+=(_,_)=>warning.DialogResult=false;
        Shortcut(warning,(Key.C,proceed),(Key.E,exit));warning.ContentRendered+=(_,_)=>proceed.Focus();
        if(warning.ShowDialog()!=true)return;
        main=Load("MainWindow");panel=Find<StackPanel>(main,"CandidatePanel");
        selectAll=Find<Button>(main,"SelectAllButton");deselectAll=Find<Button>(main,"DeselectAllButton");debloat=Find<Button>(main,"DebloatButton");
        selectAll.Click+=(_,_)=>boxes.ForEach(x=>x.IsChecked=true);deselectAll.Click+=(_,_)=>boxes.ForEach(x=>x.IsChecked=false);
        debloat.Click+=async (_,_)=>await OnDebloat();
        Shortcut(main,(Key.A,selectAll),(Key.U,deselectAll),(Key.D,debloat));
        Build(await Busy("LoadingWindow",null,_=>scan.Candidates()));
        main.ContentRendered+=(_,_)=>FocusFirst();main.ShowDialog();
    }
    private void FocusFirst()
    {
        if(boxes.Count>0){boxes[0].IsTabStop=true;boxes[0].Focus();boxes[0].BringIntoView();}
        else selectAll.Focus();
    }
    private void Build(List<Candidate> items)
    {
        panel.Children.Clear();boxes.Clear();
        selectAll.IsEnabled=deselectAll.IsEnabled=debloat.IsEnabled=items.Count>0;
        if(items.Count==0){panel.Children.Add(new TextBlock{Text="No removable applications or components were found.",FontSize=16,Margin=new Thickness(8),TextWrapping=TextWrapping.Wrap});return;}
        foreach(var (category,title) in Categories)
        {
            var members=items.Where(x=>x.Category==category).OrderBy(x=>x.DisplayName).ToList();if(members.Count==0)continue;
            panel.Children.Add(new TextBlock{Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(5,16,5,7),Focusable=false});
            bool first=true;
            foreach(var c in members)
            {
                var box=new CheckBox{Content=c.DisplayName,Tag=c.Id,FontSize=16,Margin=new Thickness(20,5,5,5),Padding=new Thickness(3),IsTabStop=false};
                AutomationProperties.SetName(box,first?$"{title}, {c.DisplayName}":c.DisplayName);first=false;
                box.GotKeyboardFocus+=(_,_)=>{foreach(var other in boxes)other.IsTabStop=false;box.IsTabStop=true;};
                box.PreviewKeyDown+=(_,e)=>
                {
                    if(e.Key!=Key.Up && e.Key!=Key.Down)return;
                    var at=boxes.IndexOf(box);var next=Math.Clamp(at+(e.Key==Key.Up?-1:1),0,boxes.Count-1);
                    if(next!=at){box.IsTabStop=false;boxes[next].IsTabStop=true;boxes[next].Focus();boxes[next].BringIntoView();}
                    e.Handled=true;
                };
                panel.Children.Add(box);boxes.Add(box);
            }
        }
        if(boxes.Count>0)boxes[0].IsTabStop=true;
    }
    private async Task OnDebloat()
    {
        var selected=boxes.Where(x=>x.IsChecked==true).Select(x=>byId[(string)x.Tag]).ToList();
        if(selected.Count==0){MessageBox.Show(main,"No items are selected.","Accessible Windows Toolkit",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        var confirm=Load("ConfirmWindow");confirm.Owner=main;
        Find<TextBlock>(confirm,"ConfirmText").Text=$"You selected {selected.Count} application(s) or component(s) for removal.\r\n\r\n"+string.Join("\r\n",selected.Select(x=>"- "+x.DisplayName))+"\r\n\r\nDo you want to continue?";
        var yes=Find<Button>(confirm,"YesButton");var no=Find<Button>(confirm,"NoButton");
        yes.Click+=(_,_)=>confirm.DialogResult=true;no.Click+=(_,_)=>confirm.DialogResult=false;
        Shortcut(confirm,(Key.Y,yes),(Key.N,no));confirm.ContentRendered+=(_,_)=>no.Focus();
        if(confirm.ShowDialog()!=true)return;
        List<RemovalResult> results;
        try{results=await Busy("RemovalWindow",main,progress=>new RemovalService(scan).Remove(selected,progress!));}
        catch(Exception ex){MessageBox.Show(main,"Removal encountered an unexpected error.\n\n"+ex.Message,"Accessible Windows Toolkit",MessageBoxButton.OK,MessageBoxImage.Error);return;}
        var result=Load("ResultWindow");result.Owner=main;
        Find<TextBlock>(result,"SuccessText").Text=$"Successfully removed: {results.Count(x=>x.Status=="Removed")}";
        Find<TextBlock>(result,"FailedText").Text=$"Failed to remove: {results.Count(x=>x.Status=="Error")}";
        Find<TextBlock>(result,"AbsentText").Text=$"Already absent: {results.Count(x=>x.Status=="NotInstalled")}";
        var ok=Find<Button>(result,"ResultOkButton");ok.Click+=(_,_)=>result.Close();result.ContentRendered+=(_,_)=>ok.Focus();result.ShowDialog();
        try{Build(await Busy("LoadingWindow",main,_=>scan.Candidates()));FocusFirst();}
        catch(Exception ex){MessageBox.Show(main,"System scan failed.\n\n"+ex.Message,"Accessible Windows Toolkit",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
}
