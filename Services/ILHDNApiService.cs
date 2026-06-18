using eInvWorld.Models;
using eInvWorld.Models.Document;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;

/// <summary>
/// Abstraction over the LHDN/MyInvois HTTP client. Introduced so consumers depend on an interface
/// (DIP) and the API can be mocked in tests — a prerequisite for safely refactoring the sync helpers.
/// Implemented by <see cref="LHDNApiService"/> (a typed HttpClient).
/// </summary>
public interface ILHDNApiService
{
    string? GetSystemOnBehalfOf();
    Task<DocumentSummary?> GetDocumentDetailsWithRetryAsync(string uuid, string accessToken, int maxRetries = 5);
    Task<string> ValidateTaxpayerAsync(string tin, string idType, string idValue);
    Task<string> SubmitDocumentsAsync(List<eInvWorld.Models.JsonModels.Documents> documents, string? tin = null);
    Task<string?> GetSubmissionStatusAsync(string submissionUid, string accessToken);
    Task<DocumentSummary> PollSubmissionStatusAsync(string submissionUid, string accessToken);
    Task<List<DocumentSummary>> BulkSearchDocumentsAsync(string tin, DateTime start, DateTime end);
    Task<string> RejectDocumentAsync(string documentId, string rejectionReason, string tin);
    Task<string> CancelDocumentAsync(string documentId, string cancellationReason, string tin);
    Task<(List<DocumentSummary> Invoices, Metadata Metadata)> SearchDocumentsAsync(SearchDocumentInput input, string tin);
    Task<DocumentSummary> GetDocumentDetailsAsync(string uuid, string accessToken, string? tin = null);
    string Base64Encode(string content);
    string ComputeSHA256Hash(string content);
    Task<List<string>> GetAllUuidsForTinAsync(string tin, string accessToken);
}
