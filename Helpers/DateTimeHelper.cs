namespace eInvWorld.Helpers
{
    public static class DateTimeHelper
    {
        public static DateTime ToMalaysiaTime(DateTime utcDateTime)
        {
            var malaysiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), malaysiaTimeZone);
        }
    }

}
