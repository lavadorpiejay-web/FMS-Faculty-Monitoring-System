using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly SessionService _session;
    private readonly JsonStorageService _storage;

    public LoginViewModel(IAuthService auth, SessionService session, JsonStorageService storage)
    {
        _auth = auth;
        _session = session;
        _storage = storage;
    }

    [ObservableProperty] private string _sisId = "";
    [ObservableProperty] private string _password = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHideText))]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsNotBusy => !IsBusy;
    public string ShowHideText => IsPasswordHidden ? "Show" : "Hide";

    // No demo accounts any more; kept so LoginPage.xaml bindings still resolve.
    public bool HasDemoHint => false;
    public string DemoHint => "";

    public async Task InitializeAsync()
    {
        SisId = "";
        Password = "";
        ErrorMessage = "";
        await _storage.InitializeAsync();   // make sure local JSON structure exists
    }

    [RelayCommand]
    private Task GoToRegisterAsync() => Shell.Current.GoToAsync("Register");

    [RelayCommand]
    private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(SisId) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Enter your SIS ID and password.";
            return;
        }

        try
        {
            IsBusy = true;
            var result = await _auth.SignInAsync(SisId.Trim(), Password);
            Password = "";

            if (!result.Success || result.User is null)
            {
                ErrorMessage = result.Message;
                return;
            }

            _session.Start(result.User);
            await ((AppShell)Shell.Current).EnterRoleAsync(result.User.Role);
        }
        finally { IsBusy = false; }
    }
}
