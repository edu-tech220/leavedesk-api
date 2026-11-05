using System.ComponentModel.DataAnnotations;

namespace LeaveDesk.Api.Application.Dtos;

public record CreateLeaveRequestDto(
    [Required] int LeaveTypeId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    [MaxLength(500)] string? Reason);

public record LeaveDecisionDto(
    [Required] string RowVersion,
    [MaxLength(500)] string? Comments);

public record LeaveRejectionDto(
    [Required] string RowVersion,
    [Required, MaxLength(500)] string Comments);

public record CancelApprovedLeaveDto(
    [Required] string RowVersion);

public record LeaveRequestResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    int DepartmentId,
    int LeaveTypeId,
    string LeaveTypeCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal BusinessDays,
    string Reason,
    string Status,
    Guid? ManagerDecisionById,
    DateTime? ManagerDecisionAtUtc,
    string? ManagerComments,
    Guid? HrDecisionById,
    DateTime? HrDecisionAtUtc,
    string? HrComments,
    DateTime CreatedAtUtc,
    string RowVersion);

public record LeaveRequestQuery(
    Guid? EmployeeId = null,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20);
