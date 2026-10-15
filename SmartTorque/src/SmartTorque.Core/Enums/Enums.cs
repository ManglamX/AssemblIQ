namespace SmartTorque.Core.Enums;

public enum InstrumentType : byte
{
    Torque = 1,
    Vernier = 2
}

public enum TemplateMode : byte
{
    Torque = 1,
    Vernier = 2,
    Mixed = 3
}

public enum ToleranceMode : byte
{
    Absolute = 1,
    Percent = 2
}

public enum JobStatus : byte
{
    Pending = 1,
    Assigned = 2,
    InProgress = 3,
    Completed = 4,
    OnHold = 5,
    Rework = 6,
    Cancelled = 7
}

public enum AssignmentMode : byte
{
    Manual = 1,
    Auto = 2
}

public enum ExecutionMode : byte
{
    Manual = 1,
    Plc = 2
}

public enum SessionStatus : byte
{
    InProgress = 1,
    Completed = 2,
    Abandoned = 3,
    OnHold = 4
}

public enum SessionResult : byte
{
    Pass = 1,
    Fail = 2,
    Incomplete = 3
}

public enum StepResult : byte
{
    Pass = 1,
    Fail = 2
}

public enum BmUploadStatus : byte
{
    Uploaded = 1,
    Previewed = 2,
    Confirmed = 3,
    Failed = 4
}

public enum BmRowImportStatus : byte
{
    Ok = 1,
    Warning = 2,
    Error = 3,
    Imported = 4,
    Skipped = 5
}

public enum PriorityLevel : byte
{
    Black = 1,
    Red = 2,
    Yellow = 3,
    Green = 4,
    Blue = 5,
    White = 6
}

public enum NotificationType : byte
{
    BlackJob = 1,
    General = 2
}