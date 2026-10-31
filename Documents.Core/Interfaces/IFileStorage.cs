namespace Documents.Core.Interfaces
{
    /// <summary>Private file storage. Keys server banata hai; user input kabhi path nahi banta.</summary>
    public interface IFileStorage
    {
        /// <summary>Stream save karke naya storage key lautata hai.</summary>
        Task<string> SaveAsync(Stream content, string fileType, CancellationToken ct);

        /// <summary>Null jab file nahi mili.</summary>
        Stream? OpenRead(string storageKey);

        /// <summary>Best effort — file na ho to bhi error nahi.</summary>
        void Delete(string storageKey);
    }
}
