using FacultyMonitoring.Models;

namespace FacultyMonitoring.Services;

/// <summary>
/// Resignations, started from the Profile page.
///   Faculty      : states a reason, the department's Chairperson forwards it to the Dean, the Dean forwards it to IMS Personnel.
///   Chairperson  : states a reason, it goes straight to the Dean, who forwards it to IMS Personnel.
///   Dean         : states a reason, it goes straight to IMS Personnel.
///   READS        : states a reason, it goes straight to IMS Personnel (who approved the account).
/// IMS Personnel make it official: the account becomes inactive (it can no longer sign in and leaves the directories and the
/// schedule pickers). Each person who receives it may also decline it with a reason, and the person may withdraw it while it is open.
/// A step is skipped when nobody holds that post (for example a department with no Chairperson).
/// The approver's school and department always come from the signed-in account, never from the screen.
/// </summary>
public sealed class ResignationService
{
    public const int ReasonMaxLength = 300;

    private readonly JsonStorageService _storage;
    private readonly SessionService _session;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly IEmailService _email;

    public ResignationService(JsonStorageService storage, SessionService session, AuditService audit,
        NotificationService notifications, IEmailService email)
    {
        _storage = storage; _session = session; _audit = audit; _notifications = notifications; _email = email;
    }

    private static OperationResult Fail(string message) => new(false, message);

    public static bool CanResign(UserRole? role) => role is UserRole.Faculty or UserRole.Chairperson or UserRole.Dean or UserRole.ReadsScholar;

    public static string RouteText(UserRole role) => role switch
    {
        UserRole.Faculty => "Your resignation goes to your Chairperson, who forwards it to your Dean. IMS Personnel then make it official.",
        UserRole.Chairperson => "Your resignation goes straight to your Dean. IMS Personnel then make it official.",
        UserRole.Dean => "Your resignation goes to IMS Personnel, who make it official.",
        UserRole.ReadsScholar => "Your resignation goes to IMS Personnel, who make it official. The classes you were assigned to check are then given to another READS.",
        _ => ""
    };

    private static string RoleText(UserRole role) => role switch
    {
        UserRole.Faculty => "Faculty",
        UserRole.Chairperson => "Chairperson",
        UserRole.Dean => "Dean",
        UserRole.ImsPersonnel => "IMS Personnel",
        _ => "READS"
    };

    private static string Where(ResignationRequest r) => r.Role == UserRole.Dean ? r.School : r.Department;   // READS: "IMS Office"

    private static int Index(ResignationStage s) => s switch
    {
        ResignationStage.WaitingChairperson => 0,
        ResignationStage.WaitingDean => 1,
        ResignationStage.WaitingIms => 2,
        _ => 3
    };

    // ------------------------------------------------------------------ text for the screens

    /// <summary>One line per step the resignation goes through, for example "Dean: ✓ Forwarded by Dennis Trillo, Nov 27".</summary>
    public static string Trail(ResignationRequest r)
    {
        var steps = new List<string>();
        if (r.Role == UserRole.Faculty) steps.Add(Step(r, "Chairperson", 0, r.ChairpersonName, r.ChairpersonUtc));
        if (r.Role is UserRole.Faculty or UserRole.Chairperson) steps.Add(Step(r, "Dean", 1, r.DeanName, r.DeanUtc));
        steps.Add(Step(r, "IMS Personnel", 2, r.ImsName, r.ImsUtc));
        return string.Join("\n", steps);
    }

    private static string Step(ResignationRequest r, string label, int index, string who, DateTime? when)
    {
        if (who.Length > 0)
        {
            var verb = index == 2 ? "Made official" : "Forwarded";
            return $"{label}: ✓ {verb} by {who}, {when.GetValueOrDefault().ToLocalTime():MMM d}";
        }

        // Where the resignation is now, or where it stopped.
        var at = r.Stage switch
        {
            ResignationStage.Official => 3,
            ResignationStage.Declined or ResignationStage.Withdrawn => Index(r.StoppedAt),
            _ => Index(r.Stage)
        };
        if (index < at) return $"{label}: skipped";
        if (index == at)
        {
            return r.Stage switch
            {
                ResignationStage.Declined => $"{label}: ✕ Declined by {r.DeclinedByName}" + (r.DeclineReason.Length > 0 ? $" ({r.DeclineReason})" : ""),
                ResignationStage.Withdrawn => $"{label}: withdrawn before this step",
                _ => $"{label}: … waiting for a decision"
            };
        }
        return r.Stage is ResignationStage.Declined or ResignationStage.Withdrawn ? $"{label}: -" : $"{label}: next";
    }

    private static string WaitingText(ResignationStage s) => s switch
    {
        ResignationStage.WaitingChairperson => "your Chairperson",
        ResignationStage.WaitingDean => "your Dean",
        _ => "IMS Personnel"
    };

    // ------------------------------------------------------------------ storage helpers

    private async Task<List<ResignationRequest>> LoadAsync()
    {
        await _storage.InitializeAsync();
        return await _storage.LoadAsync<ResignationRequest>(JsonStorageService.ResignationsFile);
    }

    private async Task<List<UserAccount>> UsersAsync()
    {
        await _storage.InitializeAsync();
        return await _storage.LoadAsync<UserAccount>(JsonStorageService.UsersFile);
    }

    private static bool Has(List<UserAccount> users, UserRole role, string school, string? department, string exceptUserId) =>
        users.Any(u => u.IsUsable && u.Role == role && u.School == school
                       && (department is null || u.Department == department) && u.Id != exceptUserId);

    private static ResignationStage AfterChairperson(List<UserAccount> users, string school, string requesterId) =>
        Has(users, UserRole.Dean, school, null, requesterId) ? ResignationStage.WaitingDean : ResignationStage.WaitingIms;

    private static ResignationStage FirstStage(UserAccount who, List<UserAccount> users) => who.Role switch
    {
        UserRole.Faculty => Has(users, UserRole.Chairperson, who.School, who.Department, who.Id)
            ? ResignationStage.WaitingChairperson : AfterChairperson(users, who.School, who.Id),
        UserRole.Chairperson => AfterChairperson(users, who.School, who.Id),
        _ => ResignationStage.WaitingIms
    };

    private static bool WaitsFor(ResignationRequest r, UserAccount me) => me.Role switch
    {
        UserRole.Chairperson => r.Stage == ResignationStage.WaitingChairperson && r.School == me.School && r.Department == me.Department,
        UserRole.Dean => r.Stage == ResignationStage.WaitingDean && r.School == me.School,
        UserRole.ImsPersonnel => r.Stage == ResignationStage.WaitingIms,
        _ => false
    };

    // ------------------------------------------------------------------ notices

    private async Task NotifyStageAsync(ResignationRequest r)
    {
        var n = new AppNotification
        {
            RequestId = r.Id,
            Title = $"Resignation: {r.RequesterName} ({RoleText(r.Role)})",
            Message = $"{Where(r)}\nReason: {r.Reason}"
        };
        switch (r.Stage)
        {
            case ResignationStage.WaitingChairperson:
                n = n with { RecipientRole = UserRole.Chairperson, RecipientSchool = r.School, RecipientDepartment = r.Department, Category = NotificationService.ForApprover };
                break;
            case ResignationStage.WaitingDean:
                n = n with { RecipientRole = UserRole.Dean, RecipientSchool = r.School, Category = NotificationService.ForApprover };
                break;
            case ResignationStage.WaitingIms:
                n = n with { RecipientRole = UserRole.ImsPersonnel, Category = NotificationService.ForIms };
                break;
            default:
                return;
        }
        await _notifications.NotifyAsync(n);
    }

    private Task TellRequesterAsync(ResignationRequest r, string title, string message) =>
        _notifications.NotifyAsync(new AppNotification
        {
            RecipientUserId = r.RequesterUserId, Category = NotificationService.ForRequester, RequestId = r.Id,
            Title = title, Message = message
        });

    // ------------------------------------------------------------------ the person resigning

    /// <summary>The signed-in person's latest resignation (open or finished), or null.</summary>
    public async Task<ResignationRequest?> GetMineAsync()
    {
        var me = _session.CurrentUser;
        if (me is null || !CanResign(me.Role)) return null;
        return (await LoadAsync()).Where(r => r.RequesterUserId == me.Id).OrderByDescending(r => r.CreatedUtc).FirstOrDefault();
    }

    public async Task<OperationResult> SubmitAsync(string reason)
    {
        var me = _session.CurrentUser;
        if (me is null || !CanResign(me.Role)) return Fail("Only Faculty, Chairpersons, Deans and READS can submit a resignation.");
        reason = (reason ?? "").Trim();
        if (reason.Length == 0) return Fail("State the reason for your resignation.");
        if (reason.Length > ReasonMaxLength) return Fail($"Keep the reason within {ReasonMaxLength} characters.");

        var all = await LoadAsync();
        if (all.Any(r => r.RequesterUserId == me.Id && r.IsOpen))
            return Fail("You already have a resignation waiting for a decision.");

        var users = await UsersAsync();
        var request = new ResignationRequest
        {
            Id = ScheduleRules.NewId("R"), RequesterUserId = me.Id, RequesterName = me.FullName, Role = me.Role,
            School = me.School, Department = me.Role == UserRole.Dean ? "" : me.Department,
            Reason = reason, Stage = FirstStage(me, users), CreatedUtc = DateTime.UtcNow
        };
        all.Add(request);
        await _storage.SaveAsync(JsonStorageService.ResignationsFile, all);
        await _audit.LogAsync("Resignation submitted", $"{me.FullName} ({RoleText(me.Role)}, {Where(request)}). Reason: {reason}");
        await NotifyStageAsync(request);

        return new OperationResult(true, $"Resignation sent to {WaitingText(request.Stage)}. You can follow it below and withdraw it until IMS Personnel make it official.");
    }

    public async Task<OperationResult> WithdrawAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return Fail("Sign in again.");
        var all = await LoadAsync();
        var i = all.FindIndex(r => r.RequesterUserId == me.Id && r.IsOpen);
        if (i < 0) return Fail("You have no resignation waiting for a decision.");

        var r = all[i];
        all[i] = r with { Stage = ResignationStage.Withdrawn, StoppedAt = r.Stage, StoppedUtc = DateTime.UtcNow };
        await _storage.SaveAsync(JsonStorageService.ResignationsFile, all);
        await _audit.LogAsync("Resignation withdrawn", $"{r.RequesterName} ({RoleText(r.Role)}, {Where(r)}) withdrew the resignation.");
        await _notifications.MarkRequestHandledAsync(r.Id, NotificationService.ForApprover, NotificationService.ForIms);
        return new OperationResult(true, "Your resignation was withdrawn.");
    }

    // ------------------------------------------------------------------ Chairperson / Dean / IMS Personnel

    /// <summary>The resignations waiting for the signed-in Chairperson (their department's Faculty), Dean (their school) or IMS Personnel.</summary>
    public async Task<List<ResignationRequest>> GetWaitingForMeAsync()
    {
        var me = _session.CurrentUser;
        if (me is null) return new();
        return (await LoadAsync()).Where(r => WaitsFor(r, me)).OrderBy(r => r.CreatedUtc).ToList();
    }

    /// <summary>The same list as rows for the Home page. IMS Personnel also see how many classes the person still handles.</summary>
    public async Task<List<ListRow>> GetRowsForMeAsync()
    {
        var me = _session.CurrentUser;
        var rows = new List<ListRow>();
        if (me is null) return rows;

        await _storage.InitializeAsync();
        var schedules = me.Role == UserRole.ImsPersonnel
            ? await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile)
            : new List<ClassSchedule>();
        var checks = me.Role == UserRole.ImsPersonnel
            ? await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile)
            : new List<CheckAssignment>();

        foreach (var r in await GetWaitingForMeAsync())
        {
            var detail = $"{Where(r)}\nReason: {r.Reason}\n{Trail(r)}";
            if (me.Role == UserRole.ImsPersonnel && r.Role == UserRole.ReadsScholar)
            {
                var assigned = checks.Count(c => c.ReadsUserId == r.RequesterUserId);
                detail += assigned == 0
                    ? "\nNo class is assigned to this READS to check."
                    : $"\n{assigned} class check{(assigned == 1 ? "" : "s")} assigned to this READS. They are removed when you make it official, so assign those classes to another READS.";
            }
            else if (me.Role == UserRole.ImsPersonnel)
            {
                var classes = schedules.Count(s => s.FacultyUserId == r.RequesterUserId);
                detail += classes == 0
                    ? "\nNo class is assigned to this person."
                    : $"\n{classes} class{(classes == 1 ? "" : "es")} still list this person. After you make it official, reassign or remove them on the Schedules tab.";
            }
            rows.Add(new ListRow(r.Id, $"{r.RequesterName}, {RoleText(r.Role)}", detail, $"Sent {r.CreatedUtc.ToLocalTime():MMM d, h:mm tt}"));
        }
        return rows;
    }

    /// <summary>Forward (Chairperson, Dean) or make official (IMS Personnel).</summary>
    public async Task<OperationResult> ApproveAsync(string requestId)
    {
        var me = _session.CurrentUser;
        if (me is null) return Fail("Sign in again.");
        var all = await LoadAsync();
        var i = all.FindIndex(r => r.Id == requestId);
        if (i < 0 || !WaitsFor(all[i], me)) return Fail("That resignation is not waiting for you.");

        var r = all[i];
        var now = DateTime.UtcNow;
        var users = await UsersAsync();

        if (me.Role == UserRole.ImsPersonnel) return await MakeOfficialAsync(all, i, users, me, now);

        ResignationRequest next;
        string forwardedTo;
        if (me.Role == UserRole.Chairperson)
        {
            var stage = AfterChairperson(users, r.School, r.RequesterUserId);
            next = r with { Stage = stage, ChairpersonName = me.FullName, ChairpersonUtc = now };
            forwardedTo = stage == ResignationStage.WaitingDean ? "the Dean" : "IMS Personnel";
        }
        else
        {
            next = r with { Stage = ResignationStage.WaitingIms, DeanName = me.FullName, DeanUtc = now };
            forwardedTo = "IMS Personnel";
        }

        all[i] = next;
        await _storage.SaveAsync(JsonStorageService.ResignationsFile, all);
        await _audit.LogAsync("Resignation forwarded", $"{r.RequesterName} ({RoleText(r.Role)}, {Where(r)}) forwarded to {forwardedTo} by {me.FullName}.");
        await _notifications.MarkRequestHandledAsync(r.Id, NotificationService.ForApprover);
        await NotifyStageAsync(next);
        await TellRequesterAsync(r, $"Your resignation was forwarded to {forwardedTo}",
            $"{RoleText(me.Role)} {me.FullName} forwarded it. IMS Personnel make it official at the end.");

        return new OperationResult(true, $"Forwarded to {forwardedTo}. {r.RequesterName} was told.");
    }

    private async Task<OperationResult> MakeOfficialAsync(List<ResignationRequest> all, int i, List<UserAccount> users, UserAccount me, DateTime now)
    {
        var r = all[i];
        var ui = users.FindIndex(u => u.Id == r.RequesterUserId);
        if (ui >= 0)
        {
            users[ui] = users[ui] with { IsActive = false };
            await _storage.SaveAsync(JsonStorageService.UsersFile, users);
        }

        all[i] = r with { Stage = ResignationStage.Official, ImsName = me.FullName, ImsUtc = now };
        await _storage.SaveAsync(JsonStorageService.ResignationsFile, all);
        await _audit.LogAsync("Resignation made official", $"{r.RequesterName} ({RoleText(r.Role)}, {Where(r)}). The account is now inactive.");
        await _notifications.MarkRequestHandledAsync(r.Id, NotificationService.ForIms);
        await TellRequesterAsync(r, "Your resignation is official",
            "IMS Personnel made your resignation official. Your account is now inactive.");

        // Best effort: a failed email never undoes the decision.
        if (ui >= 0)
        {
            try
            {
                await _email.SendAsync(users[ui].Email, "FMS: your resignation is official",
                    $"Hello {r.RequesterName},\n\nIMS Personnel made your resignation official. Your FMS account is now inactive.\n\nFMS Faculty Monitoring System");
            }
            catch (Exception) { }
        }

        var message = $"{r.RequesterName}'s resignation is official. The account is now inactive.";
        var schedules = await _storage.LoadAsync<ClassSchedule>(JsonStorageService.SchedulesFile);
        var classes = schedules.Count(s => s.FacultyUserId == r.RequesterUserId);
        if (classes > 0)
            message += $" {classes} class{(classes == 1 ? "" : "es")} still list {r.RequesterName}: reassign or remove them on the Schedules tab.";
        if (r.Role == UserRole.ReadsScholar)
        {
            var checks = await _storage.LoadAsync<CheckAssignment>(JsonStorageService.CheckAssignmentsFile);
            var removed = checks.RemoveAll(c => c.ReadsUserId == r.RequesterUserId);
            if (removed > 0)
            {
                await _storage.SaveAsync(JsonStorageService.CheckAssignmentsFile, checks);
                message += $" {removed} class check{(removed == 1 ? " was" : "s were")} taken off them: assign {(removed == 1 ? "it" : "those classes")} to another READS on the Assign tab.";
            }
        }
        if (r.Role == UserRole.Chairperson && !Has(users.Select(u => u.Id == r.RequesterUserId ? u with { IsActive = false } : u).ToList(), UserRole.Chairperson, r.School, r.Department, ""))
            message += $" The {r.Department} has no Chairperson now.";
        if (r.Role == UserRole.Dean && !users.Any(u => u.IsUsable && u.Role == UserRole.Dean && u.School == r.School && u.Id != r.RequesterUserId))
            message += $" The {r.School} has no Dean now.";
        return new OperationResult(true, message);
    }

    public async Task<OperationResult> DeclineAsync(string requestId, string reason)
    {
        var me = _session.CurrentUser;
        if (me is null) return Fail("Sign in again.");
        reason = (reason ?? "").Trim();
        if (reason.Length == 0) return Fail("Give a reason for declining.");

        var all = await LoadAsync();
        var i = all.FindIndex(r => r.Id == requestId);
        if (i < 0 || !WaitsFor(all[i], me)) return Fail("That resignation is not waiting for you.");

        var r = all[i];
        var role = RoleText(me.Role);
        all[i] = r with
        {
            Stage = ResignationStage.Declined, StoppedAt = r.Stage, StoppedUtc = DateTime.UtcNow,
            DeclinedByName = me.FullName, DeclinedByRole = role, DeclineReason = reason
        };
        await _storage.SaveAsync(JsonStorageService.ResignationsFile, all);
        await _audit.LogAsync("Resignation declined", $"{r.RequesterName} ({RoleText(r.Role)}, {Where(r)}) declined by {role} {me.FullName}. Reason: {reason}");
        await _notifications.MarkRequestHandledAsync(r.Id, NotificationService.ForApprover, NotificationService.ForIms);
        await TellRequesterAsync(r, $"Your resignation was declined by the {role}", $"{role} {me.FullName}: {reason}");
        return new OperationResult(true, $"Declined. {r.RequesterName} will see your reason.");
    }
}
