namespace Documents.Core.Options
{
    public class AppOptions
    {
        public const string SectionName = "App";
        /// <summary>Reset link ka base, e.g. http://localhost:5500</summary>
        public string WebBaseUrl { get; set; } = "";
        /// <summary>"Added this month" isi time zone mein gina jata hai.</summary>
        public string TimeZone { get; set; } = "India Standard Time";
    }
}
