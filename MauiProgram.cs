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
        builder.Services.AddSingleton<ClockService>();
        builder.Services.AddSingleton<AuditService>();
        builder.Services.AddSingleton<ScheduleService>();
        builder.Services.AddSingleton<DutyService>();
        builder.Services.AddSingleton<ChangeRequestService>();
        builder.Services.AddSingleton<AttendanceService>();

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
        builder.Services.AddTransient<SchedulesViewModel>();
        builder.Services.AddTransient<RoomsViewModel>();
        builder.Services.AddTransient<DutyViewModel>();
        builder.Services.AddTransient<ChangesViewModel>();
        builder.Services.AddTransient<MasterViewModel>();
        builder.Services.AddTransient<FacultyScheduleViewModel>();
        builder.Services.AddTransient<RequestsViewModel>();
        builder.Services.AddTransient<MyDutyViewModel>();
        builder.Services.AddTransient<AttendanceLogViewModel>();
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
        builder.Services.AddTransient<SchedulesPage>();
        builder.Services.AddTransient<RoomsPage>();
        builder.Services.AddTransient<DutyPage>();
        builder.Services.AddTransient<ChangesPage>();
        builder.Services.AddTransient<MasterPage>();
        builder.Services.AddTransient<FacultySchedulePage>();
        builder.Services.AddTransient<RequestsPage>();
        builder.Services.AddTransient<MyDutyPage>();
        builder.Services.AddTransient<AttendanceLogPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
