using Newtonsoft.Json;

namespace eInvWorld.Models
{
    public class TurnstileResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("challenge_ts")]
        public DateTime? ChallengeTs { get; set; }

        [JsonProperty("hostname")]
        public string? Hostname { get; set; }

        [JsonProperty("error-codes")]
        public List<string>? ErrorCodes { get; set; }
    }
}
