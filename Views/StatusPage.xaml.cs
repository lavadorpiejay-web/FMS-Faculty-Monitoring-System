using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class StatusPage : ContentPage
{
    private readonly StatusViewModel _vm;

    public StatusPage(StatusViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RunChecksAsync();
    }
}
