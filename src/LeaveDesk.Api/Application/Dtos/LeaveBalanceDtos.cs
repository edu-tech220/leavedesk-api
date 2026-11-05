namespace LeaveDesk.Api.Application.Dtos;

public record LeaveBalanceResponse(
    int LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    int Year,
    decimal AccruedDays,
    decimal UsedDays,
    decimal AvailableDays);
