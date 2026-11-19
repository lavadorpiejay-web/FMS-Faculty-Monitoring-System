using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class MyDutyPage : ContentPage
{
    private readonly MyDutyViewModel _vm;

    public MyDutyPage(MyDutyViewModel vm)
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
