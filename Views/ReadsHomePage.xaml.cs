using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class ReadsHomePage : ContentPage
{
    private readonly ReadsHomeViewModel _vm;

    public ReadsHomePage(ReadsHomeViewModel vm)
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
