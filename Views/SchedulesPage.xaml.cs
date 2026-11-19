using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class SchedulesPage : ContentPage
{
    private readonly SchedulesViewModel _vm;

    public SchedulesPage(SchedulesViewModel vm)
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
