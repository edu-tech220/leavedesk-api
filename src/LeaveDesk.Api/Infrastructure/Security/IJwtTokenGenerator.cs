using LeaveDesk.Api.Domain.Entities;

namespace LeaveDesk.Api.Infrastructure.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(Employee employee);
}
