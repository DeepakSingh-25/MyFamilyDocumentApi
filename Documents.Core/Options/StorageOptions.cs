namespace Documents.Core.Options
{
    public class StorageOptions
    {
        public const string SectionName = "Storage";
        /// <summary>Private folder — repo aur web root ke bahar.</summary>
        public string RootPath { get; set; } = "";
    }
}
