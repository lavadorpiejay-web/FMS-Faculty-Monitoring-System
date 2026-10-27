using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class SchoolDirectoryPage : ContentPage
{
    private readonly SchoolDirectoryViewModel _vm;

    public SchoolDirectoryPage(SchoolDirectoryViewModel vm)
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
