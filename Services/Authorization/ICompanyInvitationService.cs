using System;
using System.Threading;
using System.Threading.Tasks;
using eInvWorld.Models;

namespace EINVWORLD.Services.Authorization
{
    public sealed record InvitationValidationResult(bool IsValid, string? Error, CompanyInvitation? Invitation);

    /// <summary>
    /// Creates and redeems company invitations. Never generates or stores a password — the invitee
    /// always sets their own via the normal Identity flow when accepting.
    /// </summary>
    public interface ICompanyInvitationService
    {
        /// <summary>Creates an invitation and returns it plus the one-time raw token (never persisted) so the caller can build the accept link and send the email.</summary>
        Task<(CompanyInvitation Invitation, string RawToken)> CreateAsync(int partyInfoId, string email, int? companyRoleId, string invitedByUserId, CancellationToken ct = default);

        /// <summary>Validates a raw token against the stored hash — checks existence, expiry, and not-already-used/-revoked.</summary>
        Task<InvitationValidationResult> ValidateAsync(int invitationId, string rawToken, CancellationToken ct = default);

        /// <summary>Marks an invitation accepted. Caller is responsible for creating the UserCompany row first.</summary>
        Task MarkAcceptedAsync(int invitationId, CancellationToken ct = default);

        Task RevokeAsync(int invitationId, int partyInfoId, CancellationToken ct = default);
    }
}
