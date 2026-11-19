using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class DutyPage : ContentPage
{
    private readonly DutyViewModel _vm;

    public DutyPage(DutyViewModel vm)
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
