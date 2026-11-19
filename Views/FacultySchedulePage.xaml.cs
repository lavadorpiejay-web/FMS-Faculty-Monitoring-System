using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class FacultySchedulePage : ContentPage
{
    private readonly FacultyScheduleViewModel _vm;

    public FacultySchedulePage(FacultyScheduleViewModel vm)
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
