using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class ApprovalsHomePage : ContentPage
{
    private readonly ApprovalsHomeViewModel _vm;

    public ApprovalsHomePage(ApprovalsHomeViewModel vm)
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
