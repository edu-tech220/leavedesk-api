using LeaveDesk.Api.Domain.Enums;
using LeaveDesk.Api.Domain.Exceptions;

namespace LeaveDesk.Api.Domain.Entities;

public class LeaveRequest : IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal BusinessDays { get; set; }
    public string Reason { get; set; } = string.Empty;

    public LeaveRequestStatus Status { get; set; }

    public Guid? ManagerDecisionById { get; set; }
    public DateTime? ManagerDecisionAtUtc { get; set; }
    public string? ManagerComments { get; set; }

    public Guid? HrDecisionById { get; set; }
    public DateTime? HrDecisionAtUtc { get; set; }
    public string? HrComments { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Token de concurrencia optimista: evita que dos decisiones pisen la misma solicitud.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public static LeaveRequest Create(
        Guid employeeId,
        LeaveType leaveType,
        DateTime startDate,
        DateTime endDate,
        decimal businessDays,
        string reason,
        DateTime nowUtc)
    {
        if (startDate.Date > endDate.Date)
        {
            throw new BusinessRuleException("La fecha de inicio no puede ser posterior a la fecha de fin.");
        }

        if (startDate.Date < nowUtc.Date)
        {
            throw new BusinessRuleException("No se pueden solicitar permisos con fecha de inicio en el pasado.");
        }

        if (businessDays <= 0)
        {
            throw new BusinessRuleException("El rango seleccionado no contiene dias habiles.");
        }

        return new LeaveRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            LeaveTypeId = leaveType.Id,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            BusinessDays = businessDays,
            Reason = reason,
            Status = LeaveRequestStatus.PendingManager,
            CreatedAtUtc = nowUtc
        };
    }

    public bool OverlapsWith(DateTime otherStart, DateTime otherEnd)
        => StartDate.Date <= otherEnd.Date && EndDate.Date >= otherStart.Date;

    public bool IsOpen => Status is LeaveRequestStatus.PendingManager or LeaveRequestStatus.PendingHr;

    public void ApproveByManager(Guid managerId, string? comments, bool requiresHrApproval, DateTime nowUtc)
    {
        EnsureStatus(LeaveRequestStatus.PendingManager);

        ManagerDecisionById = managerId;
        ManagerDecisionAtUtc = nowUtc;
        ManagerComments = comments;
        Status = requiresHrApproval ? LeaveRequestStatus.PendingHr : LeaveRequestStatus.Approved;
    }

    public void RejectByManager(Guid managerId, string comments, DateTime nowUtc)
    {
        EnsureStatus(LeaveRequestStatus.PendingManager);

        ManagerDecisionById = managerId;
        ManagerDecisionAtUtc = nowUtc;
        ManagerComments = comments;
        Status = LeaveRequestStatus.Rejected;
    }

    public void ApproveByHr(Guid hrId, string? comments, DateTime nowUtc)
    {
        EnsureStatus(LeaveRequestStatus.PendingHr);

        HrDecisionById = hrId;
        HrDecisionAtUtc = nowUtc;
        HrComments = comments;
        Status = LeaveRequestStatus.Approved;
    }

    public void RejectByHr(Guid hrId, string comments, DateTime nowUtc)
    {
        EnsureStatus(LeaveRequestStatus.PendingHr);

        HrDecisionById = hrId;
        HrDecisionAtUtc = nowUtc;
        HrComments = comments;
        Status = LeaveRequestStatus.Rejected;
    }

    public void CancelWhilePending()
    {
        if (!IsOpen)
        {
            throw new ConflictException("Solo se pueden cancelar solicitudes pendientes de aprobacion.");
        }

        Status = LeaveRequestStatus.Cancelled;
    }

    public void CancelApproved(DateTime nowUtc)
    {
        if (Status != LeaveRequestStatus.Approved)
        {
            throw new ConflictException("Solo se puede cancelar un permiso ya aprobado.");
        }

        if (StartDate.Date <= nowUtc.Date)
        {
            throw new BusinessRuleException("No se puede cancelar un permiso que ya inicio.");
        }

        Status = LeaveRequestStatus.Cancelled;
    }

    private void EnsureStatus(LeaveRequestStatus expected)
    {
        if (Status != expected)
        {
            throw new ConflictException(
                $"La solicitud esta en estado '{Status}' y no admite esta accion (se esperaba '{expected}').");
        }
    }
}
