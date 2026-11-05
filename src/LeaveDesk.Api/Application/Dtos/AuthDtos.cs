using System.ComponentModel.DataAnnotations;

namespace LeaveDesk.Api.Application.Dtos;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    Guid EmployeeId,
    string FullName,
    string Role);
