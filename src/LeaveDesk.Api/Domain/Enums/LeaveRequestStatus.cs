namespace LeaveDesk.Api.Domain.Enums;

public enum LeaveRequestStatus
{
    PendingManager = 0,
    PendingHr = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}
