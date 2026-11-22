using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

/// <summary>Chairperson "My schedule" and Dean "My subjects": the classes the signed-in person handles.</summary>
public partial class MySchedulePage : ContentPage
{
    private readonly MyScheduleViewModel _vm;

    public MySchedulePage(MyScheduleViewModel vm)
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
