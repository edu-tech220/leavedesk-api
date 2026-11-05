using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Domain.Exceptions;
using LeaveDesk.Api.Infrastructure.Data;
using LeaveDesk.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Application.Services;

public class AuthService : IAuthService
{
    private readonly LeaveDeskDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(LeaveDeskDbContext context, IPasswordHasher passwordHasher, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees
            .SingleOrDefaultAsync(e => e.Email == request.Email, cancellationToken);

        if (employee is null || !_passwordHasher.Verify(request.Password, employee.PasswordHash))
        {
            throw new AuthenticationFailedException("Correo o contrasena invalidos.");
        }

        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(employee);

        return new LoginResponse(token, expiresAtUtc, employee.Id, employee.FullName, employee.Role.ToString());
    }
}
