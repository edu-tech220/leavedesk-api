namespace LeaveDesk.Api.Domain.Entities;

/// <summary>
/// Bitacora de cambios de estado. En SQL Server se llena automaticamente con un trigger
/// (ver Infrastructure/Data/Migrations) como defensa adicional independiente de la capa de aplicacion.
/// </summary>
public class LeaveRequestAudit
{
    public long Id { get; set; }
    public Guid LeaveRequestId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
