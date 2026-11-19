using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FacultyMonitoring.Services;

namespace FacultyMonitoring.ViewModels;

/// <summary>Enter the 6-digit code emailed after registering. Opened as VerifyEmail?userId=...</summary>
[QueryProperty(nameof(UserId), "userId")]
public partial class VerifyEmailViewModel : ObservableObject
{
    private readonly VerificationService _verification;
    public VerifyEmailViewModel(VerificationService verification) => _verification = verification;

    [ObservableProperty] private string _userId = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _maskedEmail = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInfo))]
    private string _info = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool HasInfo => Info.Length > 0;
    public bool HasError => ErrorMessage.Length > 0;
    public bool IsNotBusy => !IsBusy;

    partial void OnUserIdChanged(string value) => _ = LoadAsync(value);

    private async Task LoadAsync(string id)
    {
        var user = await _verification.FindUserAsync(Uri.UnescapeDataString(id));
        MaskedEmail = user is null ? "" : VerificationService.MaskEmail(user.Email);
        Code = ErrorMessage = "";
        Info = user is null ? "" : $"Enter the 6-digit code we sent to {MaskedEmail}. It expires in 10 minutes.";
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        if (Code.Trim().Length != 6) { ErrorMessage = "Enter the 6-digit code."; return; }

        try
        {
            IsBusy = true;
            var result = await _verification.VerifyEmailAsync(Uri.UnescapeDataString(UserId), Code);
            if (!result.Success) { ErrorMessage = result.Message; return; }

            await Shell.Current.DisplayAlertAsync("Email verified", result.Message, "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResendAsync()
    {
        if (IsBusy) return;
        ErrorMessage = "";
        try
        {
            IsBusy = true;
            var send = await _verification.SendRegistrationCodeAsync(Uri.UnescapeDataString(UserId));
            if (send.Simulated)
                await Shell.Current.DisplayAlertAsync("Simulated email (no mail account set up)", send.SimulatedMail, "OK");
            if (!send.Success) ErrorMessage = send.Message;
            else Info = send.Simulated ? "A new code was generated." : send.Message;
        }
        finally { IsBusy = false; }
    }
}
