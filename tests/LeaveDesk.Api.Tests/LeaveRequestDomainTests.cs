using LeaveDesk.Api.Domain.Entities;
using LeaveDesk.Api.Domain.Enums;
using LeaveDesk.Api.Domain.Exceptions;
using Xunit;

namespace LeaveDesk.Api.Tests;

/// <summary>Pruebas unitarias puras de la maquina de estados, sin base de datos ni HTTP.</summary>
public class LeaveRequestDomainTests
{
    private static readonly DateTime Today = new(2026, 11, 2); // lunes, usado como "hoy" fijo en estas pruebas
    private static readonly LeaveType Vacation = new() { Id = 1, Code = "VACATION", Name = "Vacaciones", AnnualDays = 12, TracksBalance = true };

    [Fact]
    public void Create_ConDatosValidos_QuedaPendienteDeGerente()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, "Motivo", Today);

        Assert.Equal(LeaveRequestStatus.PendingManager, request.Status);
        Assert.True(request.IsOpen);
    }

    [Fact]
    public void Create_ConFinAntesDeInicio_LanzaBusinessRuleException()
    {
        Assert.Throws<BusinessRuleException>(() =>
            LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(5), Today.AddDays(1), 3, null!, Today));
    }

    [Fact]
    public void Create_ConFechaEnElPasado_LanzaBusinessRuleException()
    {
        Assert.Throws<BusinessRuleException>(() =>
            LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(-2), Today.AddDays(1), 3, null!, Today));
    }

    [Fact]
    public void Create_SinDiasHabilesEnElRango_LanzaBusinessRuleException()
    {
        Assert.Throws<BusinessRuleException>(() =>
            LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 0, null!, Today));
    }

    [Fact]
    public void ApproveByManager_SinRequerirHr_QuedaAprobadoDirecto()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, null!, Today);

        request.ApproveByManager(Guid.NewGuid(), "ok", requiresHrApproval: false, Today);

        Assert.Equal(LeaveRequestStatus.Approved, request.Status);
    }

    [Fact]
    public void ApproveByManager_RequiriendoHr_QuedaPendienteDeHr()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(20), 10, null!, Today);

        request.ApproveByManager(Guid.NewGuid(), null, requiresHrApproval: true, Today);

        Assert.Equal(LeaveRequestStatus.PendingHr, request.Status);
        Assert.True(request.IsOpen);
    }

    [Fact]
    public void ApproveByManager_CuandoYaFueResuelta_LanzaConflictException()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, null!, Today);
        request.RejectByManager(Guid.NewGuid(), "no", Today);

        Assert.Throws<ConflictException>(() => request.ApproveByManager(Guid.NewGuid(), null, false, Today));
    }

    [Fact]
    public void ApproveByHr_CuandoNoEstaPendienteDeHr_LanzaConflictException()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, null!, Today);

        Assert.Throws<ConflictException>(() => request.ApproveByHr(Guid.NewGuid(), null, Today));
    }

    [Fact]
    public void CancelWhilePending_DesdeAprobado_LanzaConflictException()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, null!, Today);
        request.ApproveByManager(Guid.NewGuid(), null, false, Today);

        Assert.Throws<ConflictException>(() => request.CancelWhilePending());
    }

    [Fact]
    public void CancelApproved_AntesDeIniciar_Cancela()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, null!, Today);
        request.ApproveByManager(Guid.NewGuid(), null, false, Today);

        request.CancelApproved(Today);

        Assert.Equal(LeaveRequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public void CancelApproved_CuandoYaInicio_LanzaBusinessRuleException()
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(7), Today.AddDays(9), 3, null!, Today);
        request.ApproveByManager(Guid.NewGuid(), null, false, Today);

        Assert.Throws<BusinessRuleException>(() => request.CancelApproved(Today.AddDays(7)));
    }

    [Theory]
    [InlineData(5, 6, 4, 7, true)]
    [InlineData(5, 6, 7, 9, false)]
    public void OverlapsWith_DetectaSolapamientoCorrectamente(int startOffset, int endOffset, int otherStartOffset, int otherEndOffset, bool expectedOverlap)
    {
        var request = LeaveRequest.Create(Guid.NewGuid(), Vacation, Today.AddDays(startOffset), Today.AddDays(endOffset), 2, null!, Today);

        var overlaps = request.OverlapsWith(Today.AddDays(otherStartOffset), Today.AddDays(otherEndOffset));

        Assert.Equal(expectedOverlap, overlaps);
    }
}
