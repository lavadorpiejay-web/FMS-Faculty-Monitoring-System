using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class AttendanceLogPage : ContentPage
{
    private readonly AttendanceLogViewModel _vm;

    public AttendanceLogPage(AttendanceLogViewModel vm)
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
