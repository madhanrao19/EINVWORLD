using eInvWorld.Data;

namespace eInvWorld.Services.Extensions
{
    public interface IStatusMappingService
    {
        string? MapLhdnStatusToInternalStatus(string lhdnStatus);
        string GetStatusIdByCode(string statusCode);
    }

    public class StatusMappingService : IStatusMappingService
    {
        private readonly ApplicationDbContext _context;

        public StatusMappingService(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Maps LHDN status to the corresponding internal status.
        /// </summary>
        /// <param name="lhdnStatus">LHDN status from the API</param>
        /// <returns>Internal status as a string</returns>
        public string? MapLhdnStatusToInternalStatus(string lhdnStatus)
        {
            return lhdnStatus switch
            {
                "Submitted" => "Submitted",
                "Valid" => "Valid",
                "Invalid" => "Invalid",
                "Cancelled" => "Cancelled",
                _ => null // Unknown status
            };
        }

        /// <summary>
        /// Gets the StatusId for a given StatusCode.
        /// </summary>
        /// <param name="statusCode">The status code to find</param>
        /// <returns>The StatusId corresponding to the status code</returns>
        public string GetStatusIdByCode(string statusCode)
        {
            var status = _context.Statuses.FirstOrDefault(s => s.StatusCode == statusCode);
            if (status == null)
            {
                throw new KeyNotFoundException($"Status with code '{statusCode}' not found in the database.");
            }
            return status.StatusCode;
        }
    }
}
