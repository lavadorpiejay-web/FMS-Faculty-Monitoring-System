using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class ChangesPage : ContentPage
{
    private readonly ChangesViewModel _vm;

    public ChangesPage(ChangesViewModel vm)
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
