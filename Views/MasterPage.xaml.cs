using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class MasterPage : ContentPage
{
    private readonly MasterViewModel _vm;

    public MasterPage(MasterViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
