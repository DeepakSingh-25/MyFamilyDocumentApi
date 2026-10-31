namespace Documents.Core.Options
{
    public class SignupOptions
    {
        public const string SectionName = "Signup";
        /// <summary>One-time setup code (user-secrets). Khaali = sign-up band.</summary>
        public string SetupCode { get; set; } = "";
    }
}
