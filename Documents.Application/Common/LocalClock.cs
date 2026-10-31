using Documents.Core.Options;

namespace Documents.Application.Common
{
    /// <summary>App:TimeZone (default India Standard Time) ke hisaab se "aaj" aur "is mahine".</summary>
    internal static class LocalClock
    {
        public static DateOnly Today(TimeProvider clock, AppOptions app) =>
            DateOnly.FromDateTime(LocalNow(clock, app).DateTime);

        /// <summary>Local mahine ki pehli taarikh 00:00, UTC mein.</summary>
        public static DateTime MonthStartUtc(TimeProvider clock, AppOptions app)
        {
            var local = LocalNow(clock, app);
            var monthStart = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, local.Offset);
            return monthStart.UtcDateTime;
        }

        private static DateTimeOffset LocalNow(TimeProvider clock, AppOptions app) =>
            TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(app.TimeZone));
    }
}
