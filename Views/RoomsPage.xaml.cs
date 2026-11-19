using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class RoomsPage : ContentPage
{
    private readonly RoomsViewModel _vm;

    public RoomsPage(RoomsViewModel vm)
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
