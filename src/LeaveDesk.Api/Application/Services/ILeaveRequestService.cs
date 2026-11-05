using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Common;
using LeaveDesk.Api.Application.Dtos;

namespace LeaveDesk.Api.Application.Services;

public interface ILeaveRequestService
{
    Task<LeaveRequestResponse> CreateAsync(ICurrentUser user, CreateLeaveRequestDto dto, CancellationToken cancellationToken = default);

    Task<LeaveRequestResponse> GetByIdAsync(Guid id, ICurrentUser user, CancellationToken cancellationToken = default);

    Task<PagedResult<LeaveRequestResponse>> SearchAsync(ICurrentUser user, LeaveRequestQuery query, CancellationToken cancellationToken = default);

    Task<PagedResult<LeaveRequestResponse>> GetPendingApprovalsAsync(ICurrentUser user, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<LeaveRequestResponse> ApproveByManagerAsync(Guid id, ICurrentUser user, LeaveDecisionDto dto, CancellationToken cancellationToken = default);

    Task<LeaveRequestResponse> RejectByManagerAsync(Guid id, ICurrentUser user, LeaveRejectionDto dto, CancellationToken cancellationToken = default);

    Task<LeaveRequestResponse> ApproveByHrAsync(Guid id, ICurrentUser user, LeaveDecisionDto dto, CancellationToken cancellationToken = default);

    Task<LeaveRequestResponse> RejectByHrAsync(Guid id, ICurrentUser user, LeaveRejectionDto dto, CancellationToken cancellationToken = default);

    Task CancelAsync(Guid id, ICurrentUser user, CancellationToken cancellationToken = default);

    Task<LeaveRequestResponse> CancelApprovedAsync(Guid id, ICurrentUser user, CancelApprovedLeaveDto dto, CancellationToken cancellationToken = default);
}
