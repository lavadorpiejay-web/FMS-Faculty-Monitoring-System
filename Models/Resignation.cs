namespace FacultyMonitoring.Models;

/// <summary>Where a resignation is. Waiting* = needs that person's decision; Official, Declined and Withdrawn are final.</summary>
public enum ResignationStage { WaitingChairperson, WaitingDean, WaitingIms, Official, Declined, Withdrawn }

/// <summary>One resignation of a Faculty, Chairperson, Dean or READS. Saved in resignations.json.</summary>
public sealed record ResignationRequest
{
    public string Id { get; init; } = "";
    public string RequesterUserId { get; init; } = "";
    public string RequesterName { get; init; } = "";
    public UserRole Role { get; init; }
    public string School { get; init; } = "";
    public string Department { get; init; } = "";
    public string Reason { get; init; } = "";
    public ResignationStage Stage { get; init; }
    public DateTime CreatedUtc { get; init; }

    // Who forwarded it, and when.
    public string ChairpersonName { get; init; } = "";
    public DateTime? ChairpersonUtc { get; init; }
    public string DeanName { get; init; } = "";
    public DateTime? DeanUtc { get; init; }
    public string ImsName { get; init; } = "";
    public DateTime? ImsUtc { get; init; }

    // Set when it was declined or withdrawn: the step it was waiting at.
    public ResignationStage StoppedAt { get; init; }
    public DateTime? StoppedUtc { get; init; }
    public string DeclinedByName { get; init; } = "";
    public string DeclinedByRole { get; init; } = "";
    public string DeclineReason { get; init; } = "";

    /// <summary>Still waiting for a decision (the person may withdraw it).</summary>
    public bool IsOpen => Stage is ResignationStage.WaitingChairperson or ResignationStage.WaitingDean or ResignationStage.WaitingIms;
}
