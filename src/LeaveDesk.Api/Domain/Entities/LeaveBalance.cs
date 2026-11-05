namespace LeaveDesk.Api.Domain.Entities;

public class LeaveBalance : IHasConcurrencyToken
{
    public int Id { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public int Year { get; set; }
    public decimal AccruedDays { get; set; }
    public decimal UsedDays { get; set; }

    public decimal AvailableDays => AccruedDays - UsedDays;

    /// <summary>Token de concurrencia optimista para evitar descuentos de saldo en carrera.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
