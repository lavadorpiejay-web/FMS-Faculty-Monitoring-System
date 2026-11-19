namespace FacultyMonitoring.Models;

public enum AttendanceStatus { Present, Late, Absent }
public enum ChangeKind { Schedule, Room }
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

/// <summary>A READS Scholar's weekly duty: where (monitoring area) and when. Empty Department/Building/Floor = no restriction.</summary>
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

/// <summary>A time when a READS Scholar has no class. Duties must fit inside a vacant slot.</summary>
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
    public ChangeStatus Status { get; init; } = ChangeStatus.Pending;
    public DateTime CreatedUtc { get; init; }
    public DateTime? DecidedUtc { get; init; }
    public string DecidedByName { get; init; } = "";
    public DateTime? AppliedUtc { get; init; }
    public string AppliedByName { get; init; } = "";
}

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
    public AttendanceStatus Status { get; init; }
    public string CheckerUserId { get; init; } = "";
    public string CheckerName { get; init; } = "";
    public DateTime TimestampUtc { get; init; }
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
public sealed record ListRow(string Id, string Title, string Detail, string Extra = "");

/// <summary>A class a READS Scholar can see during duty, with whether it can be logged right now.</summary>
public sealed record MonitorRow(string ScheduleId, string Title, string Detail, string State, bool CanLog);
