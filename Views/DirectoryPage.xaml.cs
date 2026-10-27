using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class DirectoryPage : ContentPage
{
    private readonly DirectoryViewModel _vm;

    public DirectoryPage(DirectoryViewModel vm)
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
