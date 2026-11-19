using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Forgot password: enter the account email, get a 6-digit code, then choose a new password.</summary>
public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly VerificationService _verification;
    public ForgotPasswordViewModel(VerificationService verification) => _verification = verification;

    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _code = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchText))]
    [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
    [NotifyPropertyChangedFor(nameof(MatchText))]
    private string _newPassword = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchText))]
    [NotifyPropertyChangedFor(nameof(PasswordsMatch))]
    [NotifyPropertyChangedFor(nameof(MatchText))]
    private string _confirmPassword = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHideText))]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmShowHideText))]
    private bool _isConfirmPasswordHidden = true;

    // Step 1 asks for the email; step 2 (after the code was sent) asks for the code and the new password.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStepOne))]
    private bool _codeSent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInfo))]
    private string _info = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsStepOne => !CodeSent;
    public bool HasInfo => Info.Length > 0;
    public bool HasError => ErrorMessage.Length > 0;
    public bool IsNotBusy => !IsBusy;
    public string ShowHideText => IsPasswordHidden ? "Show" : "Hide";
    public string ConfirmShowHideText => IsConfirmPasswordHidden ? "Show" : "Hide";
    public string PasswordRule => LocalAuthService.PasswordRule;

    public bool HasMatchText => ConfirmPassword.Length > 0;
    public bool PasswordsMatch => NewPassword == ConfirmPassword;
    public string MatchText => PasswordsMatch ? "Passwords match" : "Passwords do not match";

    public void Reset()
    {
        Email = Code = NewPassword = ConfirmPassword = Info = ErrorMessage = "";
        CodeSent = false;
        IsPasswordHidden = IsConfirmPasswordHidden = true;
    }

    [RelayCommand] private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;
    [RelayCommand] private void ToggleConfirmPassword() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden;

    [RelayCommand]
    private void ChangeEmail()
    {
        CodeSent = false;
        Code = NewPassword = ConfirmPassword = Info = ErrorMessage = "";
    }

    [RelayCommand]
    private async Task SendCodeAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        try
        {
            IsBusy = true;
            var send = await _verification.SendResetCodeAsync(Email);
            if (send.Simulated)
                await Shell.Current.DisplayAlert("Simulated email (no mail account set up)", send.SimulatedMail, "OK");
            if (!send.Success) { ErrorMessage = send.Message; return; }

            Info = send.Message;
            CodeSent = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        if (Code.Trim().Length != 6) { ErrorMessage = "Enter the 6-digit code."; return; }

        try
        {
            IsBusy = true;
            var result = await _verification.ResetPasswordAsync(Email, Code, NewPassword, ConfirmPassword);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            await Shell.Current.DisplayAlert("Password changed", result.Message, "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally { IsBusy = false; }
    }
}
