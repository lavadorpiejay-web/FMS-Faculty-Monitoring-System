using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class DepartmentFacultyPage : ContentPage
{
    private readonly DepartmentFacultyViewModel _vm;

    public DepartmentFacultyPage(DepartmentFacultyViewModel vm)
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
