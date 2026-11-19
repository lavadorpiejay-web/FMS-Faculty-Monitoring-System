using FacultyMonitoring.Services;
using FacultyMonitoring.ViewModels;
using FacultyMonitoring.Views;
using Microsoft.Extensions.Logging;

namespace FacultyMonitoring;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<JsonStorageService>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<IAuthService, LocalAuthService>(); // swap for a real auth client later
        builder.Services.AddSingleton<IEmailService, EmailService>();
        builder.Services.AddSingleton<VerificationService>();
        builder.Services.AddSingleton<ApprovalService>();
        builder.Services.AddSingleton<ReadinessService>();
        builder.Services.AddSingleton<DirectoryService>();
        builder.Services.AddSingleton<ScholarAssignmentService>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        // View models + pages
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<StatusViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<DirectoryViewModel>();
        builder.Services.AddTransient<ScholarAssignmentsViewModel>();
        builder.Services.AddTransient<SchoolDirectoryViewModel>();
        builder.Services.AddTransient<DepartmentFacultyViewModel>();
        builder.Services.AddTransient<VerifyEmailViewModel>();
        builder.Services.AddTransient<ForgotPasswordViewModel>();
        builder.Services.AddTransient<ApprovalsViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<StatusPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<DirectoryPage>();
        builder.Services.AddTransient<AssignmentsPage>();
        builder.Services.AddTransient<SchoolDirectoryPage>();
        builder.Services.AddTransient<DepartmentFacultyPage>();
        builder.Services.AddTransient<VerifyEmailPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ApprovalsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
