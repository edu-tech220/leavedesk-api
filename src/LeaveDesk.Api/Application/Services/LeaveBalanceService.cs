using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Domain.Enums;
using LeaveDesk.Api.Domain.Exceptions;
using LeaveDesk.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Application.Services;

public class LeaveBalanceService : ILeaveBalanceService
{
    private readonly LeaveDeskDbContext _context;
    private readonly TimeProvider _timeProvider;

    public LeaveBalanceService(LeaveDeskDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<LeaveBalanceResponse>> GetForEmployeeAsync(
        Guid employeeId, ICurrentUser user, int? year, CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(employeeId, user, cancellationToken);

        var targetYear = year ?? _timeProvider.GetUtcNow().Year;

        var leaveTypes = await _context.LeaveTypes.AsNoTracking().ToListAsync(cancellationToken);
        var balances = await _context.LeaveBalances
            .AsNoTracking()
            .Where(b => b.EmployeeId == employeeId && b.Year == targetYear)
            .ToListAsync(cancellationToken);

        return leaveTypes
            .Where(t => t.TracksBalance)
            .Select(type =>
            {
                var balance = balances.FirstOrDefault(b => b.LeaveTypeId == type.Id);
                var accrued = balance?.AccruedDays ?? type.AnnualDays;
                var used = balance?.UsedDays ?? 0m;
                return new LeaveBalanceResponse(type.Id, type.Code, type.Name, targetYear, accrued, used, accrued - used);
            })
            .ToList();
    }

    private async Task EnsureCanViewAsync(Guid employeeId, ICurrentUser user, CancellationToken cancellationToken)
    {
        if (user.Role is Role.Hr or Role.Admin)
        {
            return;
        }

        if (employeeId == user.EmployeeId)
        {
            return;
        }

        if (user.Role == Role.Manager)
        {
            var isDirectReport = await _context.Employees
                .AnyAsync(e => e.Id == employeeId && e.ManagerId == user.EmployeeId, cancellationToken);

            if (isDirectReport)
            {
                return;
            }
        }

        throw new ForbiddenException("No tienes permiso para consultar el saldo de este empleado.");
    }
}
