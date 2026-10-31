namespace Documents.Core.Options
{
    public class SmtpOptions
    {
        public const string SectionName = "Smtp";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "no-reply@family.local";
        public bool EnableSsl { get; set; } = true;
        /// <summary>Set ho to mail bheji nahi jati, .eml file is folder mein likhi jati hai (development).</summary>
        public string PickupDirectory { get; set; } = "";
    }
}
