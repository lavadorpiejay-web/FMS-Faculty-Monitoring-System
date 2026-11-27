namespace FacultyMonitoring.Models;

public enum AttendanceStatus { Present, Late, Absent }
/// <summary>Schedule = day/time/subject change (Chairperson). Room = move a class to another room (Dean). RoomUse = IMS encoded a new class in a room on a floor
/// owned by another school: the owner's Dean must give permission before the class is saved.</summary>
public enum ChangeKind { Schedule, Room, RoomUse }
public enum ChangeStatus { Pending, Approved, Declined, Applied }
public enum IrregularityKind { FacultyNotInRoom, UnapprovedRoom, OutsideAssignment }

/// <summary>One weekly class: who teaches what, where and when. Times are "HH:mm" (24 hour) text.</summary>
public sealed record ClassSchedule
{
    public string Id { get; init; } = "";
    public string FacultyUserId { get; init; } = "";
    public string FacultyName { get; init; } = "";
    public string School { get; init; } = "";
    public string Department { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string SubjectTitle { get; init; } = "";
    public string ClassroomId { get; init; } = "";
    public DayOfWeek Day { get; init; }
    public string Start { get; init; } = "08:00";
    public string End { get; init; } = "09:00";
}

/// <summary>A READS Scholar's weekly duty: where (monitoring area: campus, building, floor) and when. Empty Building/Floor = no restriction.
/// School and Department are no longer used for matching; they stay so older saved duties still load.</summary>
public sealed record DutyAssignment
{
    public string Id { get; init; } = "";
    public string ReadsUserId { get; init; } = "";
    public string ReadsName { get; init; } = "";
    public string Campus { get; init; } = "";
    public string School { get; init; } = "";
    public string Department { get; init; } = "";
    public string Building { get; init; } = "";
    public string Floor { get; init; } = "";
    public DayOfWeek Day { get; init; }
    public string Start { get; init; } = "08:00";
    public string End { get; init; } = "09:00";
}

/// <summary>IMS Personnel assigned one class (a weekly schedule row) to a READS Scholar to check. The class lies inside one of the Scholar's
/// work times. Building and Floor are copied from the class's room so the Scholar knows which floor to go to.</summary>
public sealed record CheckAssignment
{
    public string Id { get; init; } = "";
    public string ReadsUserId { get; init; } = "";
    public string ScheduleId { get; init; } = "";
    public string Building { get; init; } = "";
    public string Floor { get; init; } = "";
    /// <summary>Which checks of the class this Scholar does: "Both", "Start" (first check only) or "End" (end check only).
    /// A class that runs past the end of a Scholar's work time is split: another Scholar takes over and does the end check.</summary>
    public string Part { get; init; } = CheckPart.Both;

    [System.Text.Json.Serialization.JsonIgnore] public bool DoesStart => Part != CheckPart.End;
    [System.Text.Json.Serialization.JsonIgnore] public bool DoesEnd => Part != CheckPart.Start;
}

public static class CheckPart
{
    public const string Both = "Both";
    public const string Start = "Start";
    public const string End = "End";
}

/// <summary>A Work Time of a READS Scholar: when they are free to work (no class). Checks must fit inside a Work Time. (Class name kept as VacantSlot so saved data still loads.)</summary>
public sealed record VacantSlot
{
    public string Id { get; init; } = "";
    public string ReadsUserId { get; init; } = "";
    public DayOfWeek Day { get; init; }
    public string Start { get; init; } = "08:00";
    public string End { get; init; } = "09:00";
}

/// <summary>A Faculty request to change a schedule (Chairperson approves) or a room (Dean approves). IMS applies it once approved.</summary>
public sealed record ChangeRequest
{
    public string Id { get; init; } = "";
    public ChangeKind Kind { get; init; }
    public string ScheduleId { get; init; } = "";
    public string ScheduleLabel { get; init; } = "";
    public string RequesterUserId { get; init; } = "";
    public string RequesterName { get; init; } = "";
    public UserRole ApproverRole { get; init; }
    public string ApproverSchool { get; init; } = "";
    public string ApproverDepartment { get; init; } = "";
    // Proposed values. Empty / null = unchanged.
    public DayOfWeek? ProposedDay { get; init; }
    public string ProposedStart { get; init; } = "";
    public string ProposedEnd { get; init; } = "";
    public string ProposedSubjectCode { get; init; } = "";
    public string ProposedClassroomId { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Summary { get; init; } = "";
    /// <summary>RoomUse only: JSON of the classes waiting for the owner's permission (see RoomUsePlan). Empty for other requests.</summary>
    public string RoomUsePlan { get; init; } = "";
    public ChangeStatus Status { get; init; } = ChangeStatus.Pending;
    public DateTime CreatedUtc { get; init; }
    public DateTime? DecidedUtc { get; init; }
    public string DecidedByName { get; init; } = "";
    public string DeclineReason { get; init; } = "";   // why IMS Personnel declined (shown to the Faculty member)
    public DateTime? AppliedUtc { get; init; }
    public string AppliedByName { get; init; } = "";
}

/// <summary>The classes IMS encoded for a room that needs the owner Dean's permission, kept inside the request until it is decided.</summary>
public sealed record RoomUsePlanData
{
    public string FacultyUserId { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string ClassroomId { get; init; } = "";
    public List<RoomUseDay> Days { get; init; } = new();
    /// <summary>READS chosen on the Schedules page (user ids; empty = none). Assigned automatically once the classes are saved.</summary>
    public string FirstReadsId { get; init; } = "";
    public string EndReadsId { get; init; } = "";
}

public sealed record RoomUseDay(DayOfWeek Day, string Start, string End);

/// <summary>One digital attendance entry. Everything the checklist asks to be associated with it is stored on the record.</summary>
public sealed record AttendanceLog
{
    public string Id { get; init; } = "";
    public string ScheduleId { get; init; } = "";
    public string FacultyUserId { get; init; } = "";
    public string FacultyName { get; init; } = "";
    public string SubjectCode { get; init; } = "";
    public string SubjectTitle { get; init; } = "";
    public string ClassroomId { get; init; } = "";
    public string RoomLabel { get; init; } = "";
    public string Campus { get; init; } = "";
    public string School { get; init; } = "";
    public string Department { get; init; } = "";
    public string Building { get; init; } = "";
    public string Date { get; init; } = "";             // monitoring date, yyyy-MM-dd
    public string ScheduledStart { get; init; } = "";
    public string ScheduledEnd { get; init; } = "";
    /// <summary>Present, Late or Absent. After the first check it is provisional; it is final once the end check is done.</summary>
    public AttendanceStatus Status { get; init; }
    public string CheckerUserId { get; init; } = "";
    public string CheckerName { get; init; } = "";
    public DateTime TimestampUtc { get; init; }
    /// <summary>The Scholar who did the end check, when it was a different Scholar than the first check (shift handover). Empty = same checker.</summary>
    public string EndCheckerUserId { get; init; } = "";
    public string EndCheckerName { get; init; } = "";

    // Two checks per class: 5 minutes after the start and in the last 15 minutes before the end.
    public bool? StartInRoom { get; init; }
    public DateTime? StartCheckUtc { get; init; }
    public bool? EndInRoom { get; init; }
    public DateTime? EndCheckUtc { get; init; }
    /// <summary>What the checker observed, for example "Faculty not in the classroom".</summary>
    public string Observation { get; init; } = "";
    public string Notes { get; init; } = "";
    /// <summary>True when the checker changed the status by hand (for example Present although the faculty was out for a bathroom break).</summary>
    public bool StatusAdjusted { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsComplete => EndCheckUtc is not null;
}

public sealed record IrregularityReport
{
    public string Id { get; init; } = "";
    public IrregularityKind Kind { get; init; }
    public string ScheduleId { get; init; } = "";
    public string FacultyName { get; init; } = "";
    public string ScheduleLabel { get; init; } = "";
    public string ExpectedRoom { get; init; } = "";
    public string ObservedRoom { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Date { get; init; } = "";
    public bool Flagged { get; init; }
    public string FlagReason { get; init; } = "";
    public string CheckerUserId { get; init; } = "";
    public string CheckerName { get; init; } = "";
    public DateTime TimestampUtc { get; init; }
}

/// <summary>Who changed what and when (schedule, room, duty and approval changes).</summary>
public sealed record AuditEntry
{
    public string Id { get; init; } = "";
    public DateTime TimestampUtc { get; init; }
    public string ActorUserId { get; init; } = "";
    public string ActorName { get; init; } = "";
    public string ActorRole { get; init; } = "";
    public string Action { get; init; } = "";
    public string Details { get; init; } = "";
}

/// <summary>A generic two-line list item. Id is what a Remove / Edit / Approve button acts on.</summary>
/// <summary>
/// One notification (the bell). It is addressed either to one person (RecipientUserId) or to a role, optionally narrowed to a
/// school and department (for example "the Chairperson of this department", or "IMS Personnel").
/// Older records with no recipient at all belong to IMS Personnel.
/// </summary>
public sealed record AppNotification
{
    public string Id { get; init; } = "";
    public string RecipientUserId { get; init; } = "";
    public UserRole? RecipientRole { get; init; }
    public string RecipientSchool { get; init; } = "";
    public string RecipientDepartment { get; init; } = "";
    public string Category { get; init; } = "";
    public string RequestId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
    public bool IsRead { get; init; }
}

/// <summary>One line of the Notifications page.</summary>
public sealed record NotificationRow(string Id, string Title, string Message, string TimeText, bool IsUnread)
{
    public bool IsRead => !IsUnread;
    public Microsoft.Maui.Graphics.Color BackgroundColor =>
        IsUnread ? Microsoft.Maui.Graphics.Color.FromArgb("#FFF1BF") : Microsoft.Maui.Graphics.Colors.White;
}

/// <summary>One block of the weekly timetable (a class, or a vacant time). Times are "HH:mm". ColorKey picks the block colour.</summary>
public sealed record TimetableBlock(string Id, DayOfWeek Day, string Start, string End, string Title,
    string Line2 = "", string Line3 = "", string ColorKey = "");

public sealed record ListRow(string Id, string Title, string Detail, string Extra = "");

/// <summary>One row of the IMS Rooms table: a room on a day, in a time, either Vacant or used by a class.</summary>
public sealed record RoomAvailabilityRow(string Room, string Day, string Time, string Availability, string Faculty,
    string Subject, string SchoolDepartment, bool IsVacant)
{
    public Microsoft.Maui.Graphics.Color StatusColor => IsVacant
        ? Microsoft.Maui.Graphics.Colors.ForestGreen : Microsoft.Maui.Graphics.Colors.Firebrick;
}

/// <summary>A class a READS Scholar can see during duty, with whether it can be logged right now.</summary>
/// <summary>Stage: 0 = nothing to do now, 1 = first check (5 minutes after the start) is open, 2 = end check (last 15 minutes before the end) is open.</summary>
public sealed record MonitorRow(string ScheduleId, string Title, string Detail, string State, bool CanLog, int Stage = 0)
{
    // Separate columns for the table on the READS Home page.
    public string Room { get; init; } = "";
    public string Time { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Faculty { get; init; } = "";
    public string FacultyUserId { get; init; } = "";
    /// <summary>Faculty, Chairperson or Dean: whoever handles the subject.</summary>
    public string FacultyRole { get; init; } = "Faculty";
    /// <summary>"Both", "Start" or "End": which checks of this class belong to this Scholar.</summary>
    public string Part { get; init; } = CheckPart.Both;
    public string PartText => Part == CheckPart.Start ? "Your part: first check only (another READS takes over for the end check)"
                            : Part == CheckPart.End ? "Your part: end check only (another READS does the first check)" : "";
    public bool HasPartText => PartText.Length > 0;
    public string Building { get; init; } = "";
    public string Floor { get; init; } = "";
    public string FloorText => Floor.Length == 0 ? "" : $"{Building} - Floor {Floor}";
    public string PersonText => $"{FacultyRole}: {Faculty}";
    /// <summary>Id of today's attendance entry for this class (empty until the first check is done).</summary>
    public string LogId { get; init; } = "";
    public bool HasLog => LogId.Length > 0;
    /// <summary>True once the class has an entry that is complete (end check done) or the class is over: the table shows it as Finished.</summary>
    public bool IsFinished { get; init; }
    public bool NotFinished => !IsFinished;
    /// <summary>Present / Late / Absent of a finished class.</summary>
    public string Result { get; init; } = "";
    public string FinishedText => IsFinished ? $"Finished: {Result}" : "";
    public string Notes { get; init; } = "";
    public bool HasNotes => Notes.Length > 0;
    public string CheckCaption => Stage == 1 ? "First check (5 minutes after the start)" : Stage == 2 ? "End check (last 15 minutes of the class)" : "";
}
