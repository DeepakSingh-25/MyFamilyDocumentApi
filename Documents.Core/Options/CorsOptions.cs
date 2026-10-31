namespace Documents.Core.Options
{
    public class CorsOptions
    {
        public const string SectionName = "Cors";
        public string[] Origins { get; set; } = [];
    }
}
