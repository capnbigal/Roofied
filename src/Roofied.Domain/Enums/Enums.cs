namespace Roofied.Domain.Enums;

/// <summary>Lifecycle of a report. Public surfaces only ever expose <see cref="Approved"/> reports.</summary>
public enum ReportStatus
{
    Draft = 0,
    PendingReview = 1,
    NeedsClarification = 2,
    Approved = 3,
    Rejected = 4,
    Archived = 5,
}

/// <summary>Who may see a report.</summary>
public enum ReportVisibility
{
    /// <summary>Eligible for the public map/list once approved.</summary>
    Public = 0,
    /// <summary>Shared with moderators only; never published.</summary>
    ModeratorOnly = 1,
    /// <summary>A personal draft owned by a registered user; not in the moderation queue until submitted.</summary>
    PersonalDraft = 2,
}

/// <summary>How confident the report is that spiking occurred.</summary>
public enum SuspicionLevel
{
    Unknown = 0,
    Suspected = 1,
    ConfirmedByMedicalTesting = 2,
}

/// <summary>Type of content that can be flagged or moderated.</summary>
public enum ModeratedContentType
{
    Report = 0,
    ChannelPost = 1,
    ChannelComment = 2,
}

/// <summary>State of a user-submitted content flag.</summary>
public enum FlagStatus
{
    Open = 0,
    Reviewing = 1,
    Resolved = 2,
    Dismissed = 3,
}

/// <summary>Reason a user flags content.</summary>
public enum FlagReason
{
    Other = 0,
    PersonalInformation = 1,
    AccusationOfIdentifiablePerson = 2,
    Harassment = 3,
    Spam = 4,
    Misinformation = 5,
    Inappropriate = 6,
}

/// <summary>Workflow state of a moderation case.</summary>
public enum ModerationCaseState
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
}

/// <summary>Priority bucket for moderation triage.</summary>
public enum ModerationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
}

/// <summary>Lifecycle of a community channel post.</summary>
public enum ChannelPostStatus
{
    PendingReview = 0,
    Approved = 1,
    Rejected = 2,
    Hidden = 3,
    Removed = 4,
}

/// <summary>Actions that are rate-limited for abuse prevention.</summary>
public enum RateLimitAction
{
    ReportSubmit = 0,
    SignIn = 1,
    ChannelPost = 2,
    ContentFlag = 3,
    AccountRegister = 4,
}

/// <summary>Coarse category for a help/resource entry.</summary>
public enum ResourceCategory
{
    Emergency = 0,
    Medical = 1,
    Support = 2,
    EvidencePreservation = 3,
    Legal = 4,
    General = 5,
}
