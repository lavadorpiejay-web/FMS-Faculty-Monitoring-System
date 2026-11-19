using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class RequestsPage : ContentPage
{
    private readonly RequestsViewModel _vm;

    public RequestsPage(RequestsViewModel vm)
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
