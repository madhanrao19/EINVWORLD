using Newtonsoft.Json;

namespace eInvWorld.Models.JsonModels
{
    public class JsonDateTimeUtil
    {
        public static string getJsonDate_WithHHmm(DateTime date)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                DateFormatString = "yyyy-MM-ddTHH:mmZ",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            return JsonConvert.SerializeObject((object)date, settings).Replace("\"", "");
        }

        public static string getJsonDateTime(DateTime date)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                DateFormatString = "yyyy-MM-ddTHH:mm:ssZ",
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            return JsonConvert.SerializeObject((object)date, settings).Replace("\"", "");
        }
    }
}
