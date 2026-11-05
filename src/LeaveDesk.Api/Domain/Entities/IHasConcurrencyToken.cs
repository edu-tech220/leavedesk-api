namespace LeaveDesk.Api.Domain.Entities;

/// <summary>
/// Token de concurrencia optimista gestionado por la aplicacion (no por la base de datos),
/// para que el comportamiento sea identico en SQL Server y SQLite. LeaveDeskDbContext
/// regenera el valor en cada SaveChanges de una entidad agregada o modificada.
/// </summary>
public interface IHasConcurrencyToken
{
    byte[] RowVersion { get; set; }
}
