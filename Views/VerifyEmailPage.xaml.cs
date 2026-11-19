using FacultyMonitoring.ViewModels;

namespace FacultyMonitoring.Views;

public partial class VerifyEmailPage : ContentPage
{
    private readonly VerifyEmailViewModel _vm;

    public VerifyEmailPage(VerifyEmailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
