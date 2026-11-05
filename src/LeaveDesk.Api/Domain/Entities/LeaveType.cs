namespace LeaveDesk.Api.Domain.Entities;

public class LeaveType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Dias habiles asignados por anio. Cero significa que el tipo no descuenta saldo (p. ej. sin goce de sueldo).
    /// </summary>
    public int AnnualDays { get; set; }

    public bool TracksBalance { get; set; } = true;

    /// <summary>
    /// Si es true, toda solicitud de este tipo requiere aprobacion de RH ademas del gerente,
    /// sin importar la duracion.
    /// </summary>
    public bool AlwaysRequiresHrApproval { get; set; }
}
