namespace Documents.Core.Options
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>
        /// Token expiry ke baad bhi itni der accept hota hai. Validation aur logout (revoked row kitni der rakhni hai)
        /// dono isi ek value se chalte hain, taaki dono alag na ho jayein.
        /// </summary>
        public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);
        public string SecretKey { get; set; } = null!;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int ExpiryMinutes { get; set; } = 60;
    }
}
