namespace eInvWorld.Helpers
{
    public static class DateTimeHelper
    {
        public static DateTime ToMalaysiaTime(DateTime utcDateTime)
        {
            var malaysiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), malaysiaTimeZone);
        }

        /// <summary>
        /// Malaysia wall-clock time (how invoice dates are stored) to UTC, independent of the server's time zone.
        /// A value already marked UTC is returned unchanged.
        /// </summary>
        public static DateTime MalaysiaToUtc(DateTime malaysiaDateTime)
        {
            if (malaysiaDateTime.Kind == DateTimeKind.Utc) return malaysiaDateTime;
            var malaysiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(malaysiaDateTime, DateTimeKind.Unspecified), malaysiaTimeZone);
        }
    }

}
