using ControlRoom.App.ViewModels;

namespace ControlRoom.App.Views;

public partial class RunbookDesignerPage : ContentPage
{
    public RunbookDesignerPage(RunbookDesignerViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RunbookDesignerViewModel vm)
        {
            vm.LoadThingsCommand.Execute(null);
        }
    }
}
