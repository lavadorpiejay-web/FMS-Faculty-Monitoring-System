using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class AssignmentsPage : ContentPage
{
    private readonly ScholarAssignmentsViewModel _vm;

    public AssignmentsPage(ScholarAssignmentsViewModel vm)
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
