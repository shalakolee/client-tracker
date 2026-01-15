namespace ClientTracker;

public partial class MobileShell : Shell
{
    public MobileShell()
    {
        InitializeComponent();
        BindingContext = new ViewModels.ShellViewModel();
    }
}
