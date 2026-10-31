namespace Documents.Core.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string hash);

        /// <summary>Anjaan email par bhi utna hi time lage, isliye ek dummy hash se compare.</summary>
        void VerifyDummy(string password);
    }
}
