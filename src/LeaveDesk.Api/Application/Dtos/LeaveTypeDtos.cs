namespace LeaveDesk.Api.Application.Dtos;

public record LeaveTypeResponse(int Id, string Code, string Name, int AnnualDays, bool TracksBalance, bool AlwaysRequiresHrApproval);
