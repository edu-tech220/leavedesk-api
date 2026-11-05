using System.Data;
using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Common;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Domain.Entities;
using LeaveDesk.Api.Domain.Enums;
using LeaveDesk.Api.Domain.Exceptions;
using LeaveDesk.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LeaveDesk.Api.Application.Services;

public class LeaveRequestService : ILeaveRequestService
{
    /// <summary>A partir de este numero de dias habiles, el gerente ya no puede aprobar solo: se exige RH.</summary>
    private const decimal ManagerOnlyApprovalThresholdDays = 5m;

    private readonly LeaveDeskDbContext _context;
    private readonly TimeProvider _timeProvider;

    public LeaveRequestService(LeaveDeskDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<LeaveRequestResponse> CreateAsync(ICurrentUser user, CreateLeaveRequestDto dto, CancellationToken cancellationToken = default)
    {
        var leaveType = await _context.LeaveTypes.FindAsync([dto.LeaveTypeId], cancellationToken)
            ?? throw new NotFoundException($"No existe el tipo de permiso {dto.LeaveTypeId}.");

        var businessDays = BusinessDayCalculator.CountBusinessDays(dto.StartDate, dto.EndDate);

        var leaveRequest = LeaveRequest.Create(
            user.EmployeeId, leaveType, dto.StartDate, dto.EndDate, businessDays, dto.Reason ?? string.Empty, UtcNow);

        await EnsureNoOverlapAsync(user.EmployeeId, leaveRequest.StartDate, leaveRequest.EndDate, excludeId: null, cancellationToken);

        if (leaveType.TracksBalance)
        {
            await EnsureSufficientBalanceAsync(user.EmployeeId, leaveType, leaveRequest.StartDate.Year, businessDays, excludeId: null, cancellationToken);
        }

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return await LoadResponseAsync(leaveRequest.Id, cancellationToken);
    }

    public async Task<LeaveRequestResponse> GetByIdAsync(Guid id, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);
        EnsureCanView(leaveRequest, user);
        return ToResponse(leaveRequest);
    }

    public async Task<PagedResult<LeaveRequestResponse>> SearchAsync(ICurrentUser user, LeaveRequestQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = _context.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .AsQueryable();

        baseQuery = user.Role switch
        {
            Role.Employee => ApplyEmployeeScope(baseQuery, user, query.EmployeeId),
            Role.Manager => ApplyManagerScope(baseQuery, user, query.EmployeeId),
            Role.Hr or Role.Admin => ApplyPrivilegedScope(baseQuery, query.EmployeeId),
            _ => throw new ForbiddenException("Rol no reconocido.")
        };

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<LeaveRequestStatus>(query.Status, true, out var status))
            {
                throw new BusinessRuleException($"Estado '{query.Status}' no es valido.");
            }

            baseQuery = baseQuery.Where(r => r.Status == status);
        }

        if (query.From.HasValue)
        {
            baseQuery = baseQuery.Where(r => r.EndDate >= query.From.Value.Date);
        }

        if (query.To.HasValue)
        {
            baseQuery = baseQuery.Where(r => r.StartDate <= query.To.Value.Date);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LeaveRequestResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<LeaveRequestResponse>> GetPendingApprovalsAsync(ICurrentUser user, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<LeaveRequest> query = _context.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType);

        query = user.Role switch
        {
            Role.Manager => query.Where(r => r.Status == LeaveRequestStatus.PendingManager && r.Employee!.ManagerId == user.EmployeeId),
            Role.Hr => query.Where(r => r.Status == LeaveRequestStatus.PendingHr),
            Role.Admin => query.Where(r => r.Status == LeaveRequestStatus.PendingManager || r.Status == LeaveRequestStatus.PendingHr),
            _ => throw new ForbiddenException("Este rol no tiene aprobaciones pendientes asignadas.")
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LeaveRequestResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<LeaveRequestResponse> ApproveByManagerAsync(Guid id, ICurrentUser user, LeaveDecisionDto dto, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);
        EnsureIsDirectManagerOrAdmin(leaveRequest, user);
        ApplyRowVersion(leaveRequest, dto.RowVersion);

        var requiresHr = leaveRequest.LeaveType!.AlwaysRequiresHrApproval || leaveRequest.BusinessDays > ManagerOnlyApprovalThresholdDays;
        leaveRequest.ApproveByManager(user.EmployeeId, dto.Comments, requiresHr, UtcNow);

        if (leaveRequest.Status == LeaveRequestStatus.Approved)
        {
            await FinalizeApprovalAsync(leaveRequest, cancellationToken);
        }
        else
        {
            await SaveWithConcurrencyGuardAsync(cancellationToken);
        }

        return await LoadResponseAsync(leaveRequest.Id, cancellationToken);
    }

    public async Task<LeaveRequestResponse> RejectByManagerAsync(Guid id, ICurrentUser user, LeaveRejectionDto dto, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);
        EnsureIsDirectManagerOrAdmin(leaveRequest, user);
        ApplyRowVersion(leaveRequest, dto.RowVersion);

        leaveRequest.RejectByManager(user.EmployeeId, dto.Comments, UtcNow);
        await SaveWithConcurrencyGuardAsync(cancellationToken);

        return await LoadResponseAsync(leaveRequest.Id, cancellationToken);
    }

    public async Task<LeaveRequestResponse> ApproveByHrAsync(Guid id, ICurrentUser user, LeaveDecisionDto dto, CancellationToken cancellationToken = default)
    {
        EnsureHrOrAdmin(user);
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);
        ApplyRowVersion(leaveRequest, dto.RowVersion);

        leaveRequest.ApproveByHr(user.EmployeeId, dto.Comments, UtcNow);
        await FinalizeApprovalAsync(leaveRequest, cancellationToken);

        return await LoadResponseAsync(leaveRequest.Id, cancellationToken);
    }

    public async Task<LeaveRequestResponse> RejectByHrAsync(Guid id, ICurrentUser user, LeaveRejectionDto dto, CancellationToken cancellationToken = default)
    {
        EnsureHrOrAdmin(user);
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);
        ApplyRowVersion(leaveRequest, dto.RowVersion);

        leaveRequest.RejectByHr(user.EmployeeId, dto.Comments, UtcNow);
        await SaveWithConcurrencyGuardAsync(cancellationToken);

        return await LoadResponseAsync(leaveRequest.Id, cancellationToken);
    }

    public async Task CancelAsync(Guid id, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);

        if (leaveRequest.EmployeeId != user.EmployeeId && user.Role != Role.Admin)
        {
            throw new ForbiddenException("Solo el solicitante o un administrador pueden cancelar esta solicitud.");
        }

        leaveRequest.CancelWhilePending();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeaveRequestResponse> CancelApprovedAsync(Guid id, ICurrentUser user, CancelApprovedLeaveDto dto, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);

        var isOwner = leaveRequest.EmployeeId == user.EmployeeId;
        var isDirectManager = user.Role == Role.Manager && leaveRequest.Employee!.ManagerId == user.EmployeeId;
        var isPrivileged = user.Role is Role.Hr or Role.Admin;

        if (!isOwner && !isDirectManager && !isPrivileged)
        {
            throw new ForbiddenException("No tienes permiso para cancelar este permiso aprobado.");
        }

        ApplyRowVersion(leaveRequest, dto.RowVersion);
        leaveRequest.CancelApproved(UtcNow);

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            if (leaveRequest.LeaveType!.TracksBalance)
            {
                var balance = await GetOrCreateBalanceAsync(leaveRequest.EmployeeId, leaveRequest.LeaveType, leaveRequest.StartDate.Year, cancellationToken);
                balance.UsedDays -= leaveRequest.BusinessDays;
                if (balance.UsedDays < 0)
                {
                    balance.UsedDays = 0;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("La solicitud o el saldo fueron modificados por otro proceso. Vuelve a intentarlo.");
        }

        return await LoadResponseAsync(leaveRequest.Id, cancellationToken);
    }

    // ----- Reglas de negocio internas -----

    private async Task FinalizeApprovalAsync(LeaveRequest leaveRequest, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            var departmentId = leaveRequest.Employee!.DepartmentId;
            var department = await _context.Departments.FindAsync([departmentId], cancellationToken)
                ?? throw new NotFoundException($"No existe el departamento {departmentId}.");

            var overlappingApproved = await CountOverlappingApprovedAsync(
                departmentId, leaveRequest.Id, leaveRequest.StartDate, leaveRequest.EndDate, cancellationToken);

            if (overlappingApproved + 1 > department.MaxConcurrentLeaves)
            {
                throw new BusinessRuleException(
                    $"No se puede aprobar: el departamento '{department.Name}' ya alcanzo su cupo de {department.MaxConcurrentLeaves} permiso(s) simultaneo(s) en ese rango de fechas.");
            }

            if (leaveRequest.LeaveType!.TracksBalance)
            {
                var balance = await GetOrCreateBalanceAsync(leaveRequest.EmployeeId, leaveRequest.LeaveType, leaveRequest.StartDate.Year, cancellationToken);
                balance.UsedDays += leaveRequest.BusinessDays;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("La solicitud o el saldo fueron modificados por otro proceso. Vuelve a intentarlo.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Cuenta permisos aprobados del departamento que se solapan con el rango dado.
    /// En SQL Server se usa WITH (UPDLOCK, HOLDLOCK) para serializar aprobaciones concurrentes
    /// del mismo departamento y evitar que dos transacciones lean el mismo cupo disponible.
    /// </summary>
    private async Task<int> CountOverlappingApprovedAsync(
        int departmentId, Guid excludeRequestId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        if (_context.Database.IsSqlServer())
        {
            var sql = @"
                SELECT COUNT(*) FROM LeaveRequests lr WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN Employees e ON e.Id = lr.EmployeeId
                WHERE e.DepartmentId = {0}
                  AND lr.Id <> {1}
                  AND lr.Status = 'Approved'
                  AND lr.StartDate <= {2}
                  AND lr.EndDate >= {3}";

            return await _context.Database.SqlQueryRaw<int>(
                sql, departmentId, excludeRequestId, endDate, startDate).SingleAsync(cancellationToken);
        }

        return await _context.LeaveRequests
            .Where(r => r.Employee!.DepartmentId == departmentId
                        && r.Id != excludeRequestId
                        && r.Status == LeaveRequestStatus.Approved
                        && r.StartDate <= endDate
                        && r.EndDate >= startDate)
            .CountAsync(cancellationToken);
    }

    private async Task<IDbContextTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken)
        => await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    private async Task EnsureNoOverlapAsync(Guid employeeId, DateTime start, DateTime end, Guid? excludeId, CancellationToken cancellationToken)
    {
        var openStatuses = new[] { LeaveRequestStatus.PendingManager, LeaveRequestStatus.PendingHr, LeaveRequestStatus.Approved };

        var overlaps = await _context.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId
            && (excludeId == null || r.Id != excludeId)
            && openStatuses.Contains(r.Status)
            && r.StartDate <= end
            && r.EndDate >= start,
            cancellationToken);

        if (overlaps)
        {
            throw new ConflictException("Ya existe una solicitud de permiso pendiente o aprobada que se solapa con esas fechas.");
        }
    }

    private async Task EnsureSufficientBalanceAsync(Guid employeeId, LeaveType leaveType, int year, decimal requestedDays, Guid? excludeId, CancellationToken cancellationToken)
    {
        var balance = await _context.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveType.Id && b.Year == year, cancellationToken);

        var accrued = balance?.AccruedDays ?? leaveType.AnnualDays;
        var used = balance?.UsedDays ?? 0m;

        // Se suma en memoria (no SumAsync) porque SQLite no traduce Sum sobre decimal; el conjunto
        // de solicitudes pendientes de un empleado para un tipo/anio es siempre pequeno.
        var reservedDaysPerRequest = await _context.LeaveRequests
            .Where(r => r.EmployeeId == employeeId
                        && r.LeaveTypeId == leaveType.Id
                        && r.StartDate.Year == year
                        && (excludeId == null || r.Id != excludeId)
                        && (r.Status == LeaveRequestStatus.PendingManager || r.Status == LeaveRequestStatus.PendingHr))
            .Select(r => r.BusinessDays)
            .ToListAsync(cancellationToken);

        var reserved = reservedDaysPerRequest.Sum();

        var available = accrued - used - reserved;

        if (requestedDays > available)
        {
            throw new BusinessRuleException(
                $"Saldo insuficiente de '{leaveType.Name}' para {year}: disponible {available} dia(s), solicitado {requestedDays} dia(s).");
        }
    }

    private async Task<LeaveBalance> GetOrCreateBalanceAsync(Guid employeeId, LeaveType leaveType, int year, CancellationToken cancellationToken)
    {
        var balance = await _context.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveType.Id && b.Year == year, cancellationToken);

        if (balance is not null)
        {
            return balance;
        }

        balance = new LeaveBalance
        {
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.Id,
            Year = year,
            AccruedDays = leaveType.AnnualDays,
            UsedDays = 0
        };

        _context.LeaveBalances.Add(balance);
        return balance;
    }

    private async Task<LeaveRequest> FindTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException($"No existe la solicitud de permiso {id}.");
    }

    private async Task<LeaveRequestResponse> LoadResponseAsync(Guid id, CancellationToken cancellationToken)
    {
        var leaveRequest = await FindTrackedAsync(id, cancellationToken);
        return ToResponse(leaveRequest);
    }

    private async Task SaveWithConcurrencyGuardAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("La solicitud fue modificada por otro proceso. Vuelve a intentarlo.");
        }
    }

    /// <summary>
    /// Le indica a EF Core cual es el RowVersion que el cliente "vio" por ultima vez, para que
    /// la clausula WHERE del UPDATE lo compare contra el valor real en la base de datos y falle
    /// con DbUpdateConcurrencyException si alguien mas ya cambio la fila (lost-update prevention).
    /// </summary>
    private void ApplyRowVersion(LeaveRequest leaveRequest, string rowVersionBase64)
    {
        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(rowVersionBase64);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("El valor de RowVersion no es un Base64 valido.");
        }

        _context.Entry(leaveRequest).Property(e => e.RowVersion).OriginalValue = rowVersion;
    }

    private static void EnsureCanView(LeaveRequest leaveRequest, ICurrentUser user)
    {
        var allowed = user.Role switch
        {
            Role.Employee => leaveRequest.EmployeeId == user.EmployeeId,
            Role.Manager => leaveRequest.EmployeeId == user.EmployeeId || leaveRequest.Employee!.ManagerId == user.EmployeeId,
            Role.Hr or Role.Admin => true,
            _ => false
        };

        if (!allowed)
        {
            throw new ForbiddenException("No tienes permiso para ver esta solicitud.");
        }
    }

    private static void EnsureIsDirectManagerOrAdmin(LeaveRequest leaveRequest, ICurrentUser user)
    {
        if (user.Role == Role.Admin)
        {
            return;
        }

        if (user.Role != Role.Manager || leaveRequest.Employee!.ManagerId != user.EmployeeId)
        {
            throw new ForbiddenException("Solo el gerente directo del empleado puede resolver esta solicitud.");
        }
    }

    private static void EnsureHrOrAdmin(ICurrentUser user)
    {
        if (user.Role is not (Role.Hr or Role.Admin))
        {
            throw new ForbiddenException("Solo Recursos Humanos puede resolver esta etapa de aprobacion.");
        }
    }

    private static IQueryable<LeaveRequest> ApplyEmployeeScope(IQueryable<LeaveRequest> query, ICurrentUser user, Guid? requestedEmployeeId)
    {
        if (requestedEmployeeId.HasValue && requestedEmployeeId.Value != user.EmployeeId)
        {
            throw new ForbiddenException("No puedes consultar solicitudes de otro empleado.");
        }

        return query.Where(r => r.EmployeeId == user.EmployeeId);
    }

    private static IQueryable<LeaveRequest> ApplyManagerScope(IQueryable<LeaveRequest> query, ICurrentUser user, Guid? requestedEmployeeId)
    {
        if (requestedEmployeeId.HasValue)
        {
            if (requestedEmployeeId.Value != user.EmployeeId)
            {
                return query.Where(r => r.EmployeeId == requestedEmployeeId.Value && r.Employee!.ManagerId == user.EmployeeId);
            }

            return query.Where(r => r.EmployeeId == user.EmployeeId);
        }

        return query.Where(r => r.EmployeeId == user.EmployeeId || r.Employee!.ManagerId == user.EmployeeId);
    }

    private static IQueryable<LeaveRequest> ApplyPrivilegedScope(IQueryable<LeaveRequest> query, Guid? requestedEmployeeId)
        => requestedEmployeeId.HasValue ? query.Where(r => r.EmployeeId == requestedEmployeeId.Value) : query;

    private static LeaveRequestResponse ToResponse(LeaveRequest r) => new(
        r.Id,
        r.EmployeeId,
        r.Employee!.FullName,
        r.Employee.DepartmentId,
        r.LeaveTypeId,
        r.LeaveType!.Code,
        r.StartDate,
        r.EndDate,
        r.BusinessDays,
        r.Reason,
        r.Status.ToString(),
        r.ManagerDecisionById,
        r.ManagerDecisionAtUtc,
        r.ManagerComments,
        r.HrDecisionById,
        r.HrDecisionAtUtc,
        r.HrComments,
        r.CreatedAtUtc,
        Convert.ToBase64String(r.RowVersion));
}
