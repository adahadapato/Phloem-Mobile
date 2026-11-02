using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services;

/// <summary>
/// Admin → Staff (GET /api/admin/staff). Read-only in the app: changing job roles, countries and what a role may do
/// is on the website. The API decides who may call it (the Staff permission).
/// </summary>
public sealed class StaffService(ApiClient api)
{
    /// <summary>The owners and everyone with a job role, owners first, then by name.</summary>
    /// <param name="ct">Cancels the call.</param>
    public Task<ApiResult<List<StaffMemberDto>>> GetStaffAsync(CancellationToken ct = default) =>
        api.GetAsync<List<StaffMemberDto>>("api/admin/staff", ct);
}
