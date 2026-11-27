using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// A Chairperson approves the Faculty of their own department; a Dean approves the Chairpersons of their own school;
/// IMS Personnel approve READS Scholars and the Chairperson of the Center for Religious Education (which has no Dean).
/// Only accounts whose email is already verified are listed. The approver's school and department always come from
/// the stored account, never from the screen.
/// </summary>
public sealed class ApprovalService
{
    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly IEmailService _email;

    public ApprovalService(JsonStorageService storage, SessionService session, IEmailService email)
    {
        _storage = storage; _session = session; _email = email;
    }

    public static string WhatApproved(UserRole approver) => approver switch
    {
        UserRole.Dean => "Chairperson",
        UserRole.ImsPersonnel => "READS and CRE",
        _ => "Faculty"
    };

    private async Task<(List<UserAccount> Users, UserAccount? Me)> LoadAsync()
    {
        await _storage.InitializeAsync();
        var users = await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
        var current = _session.CurrentUser;
        var me = current is null ? null
            : users.FirstOrDefault(u => u.Id == current.Id && u.IsUsable &&
                                        u.Role is UserRole.Chairperson or UserRole.Dean or UserRole.ImsPersonnel);
        return (users, me);
    }

    private static bool CanDecide(UserAccount me, UserAccount target) =>
        target.Status == ApprovalStatus.Pending && target.EmailVerified && target.IsActive &&
        ((me.Role == UserRole.Chairperson && target.Role == UserRole.Faculty &&
          target.School == me.School && target.Department == me.Department) ||
         (me.Role == UserRole.Dean && target.Role == UserRole.Chairperson && target.School == me.School) ||
         // The Chairperson of the Center for Religious Education has no Dean (CRE is not under a school): IMS Personnel approve it.
         (me.Role == UserRole.ImsPersonnel && target.Role == UserRole.Chairperson && AcademicCatalog.IsIndependent(target.School)) ||
         (me.Role == UserRole.ImsPersonnel && target.Role == UserRole.ReadsScholar));

    public async Task<IReadOnlyList<PendingApproval>> GetPendingAsync()
    {
        var (users, me) = await LoadAsync();
        if (me is null) return Array.Empty<PendingApproval>();
        return users.Where(u => CanDecide(me, u))
            .OrderBy(u => u.FullName)
            .Select(u => new PendingApproval(u.Id, u.FullName, u.SisId, u.Email,
                u.Role switch
                {
                    UserRole.Chairperson => $"Chairperson, {u.Department}",
                    UserRole.ReadsScholar => "READS",
                    _ => $"Faculty, {u.Department}"
                }))
            .ToList();
    }

    public async Task<(bool Ok, string Message)> DecideAsync(string userId, bool approve)
    {
        var (users, me) = await LoadAsync();
        if (me is null) return (false, "Only a signed-in Chairperson, Dean or IMS Personnel can do this.");
        var target = users.FirstOrDefault(u => u.Id == userId);
        if (target is null || !CanDecide(me, target)) return (false, "This request is no longer waiting for you.");

        users[users.IndexOf(target)] = target with { Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Declined };
        await _storage.SaveAsync(JsonStorageService.UsersFile, users);

        // Best effort: tell the applicant. A failed email never undoes the decision.
        try
        {
            var role = RolePolicy.Describe(me.Role).DisplayName;
            await _email.SendAsync(target.Email,
                approve ? "FMS: your account was approved" : "FMS: your registration was declined",
                approve
                    ? $"Hello {target.FullName},\n\nYour {role} approved your FMS account. You can now sign in.\n\nFMS Faculty Monitoring System"
                    : $"Hello {target.FullName},\n\nYour {role} declined your FMS registration. You may register again with the correct details.\n\nFMS Faculty Monitoring System");
        }
        catch (Exception) { }

        return (true, approve ? $"{target.FullName} was approved." : $"{target.FullName} was declined.");
    }
}
