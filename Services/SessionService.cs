using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

public sealed class SessionService
{
    public UserAccount? CurrentUser { get; private set; }
    public UserRole? Role => CurrentUser?.Role;
    public bool IsAuthenticated => CurrentUser is not null;

    // Credentials are never kept in the session.
    public void Start(UserAccount user) => CurrentUser = user with { PasswordHash = "", Salt = "" };
    public void End() => CurrentUser = null;
}
