using ControlRoom.App.ViewModels;

namespace ControlRoom.App.Views;

public partial class RunbooksPage : ContentPage
{
    public RunbooksPage(RunbooksViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RunbooksViewModel vm)
        {
            vm.RefreshCommand.Execute(null);
        }
    }
}
