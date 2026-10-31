using Documents.Core.Interfaces;

namespace Documents.Infrastructure.Services
{
    public class BcryptPasswordHasher : IPasswordHasher
    {
        private const int WorkFactor = 12;

        // Anjaan email par compare karne ke liye — ek baar banta hai
        private static readonly Lazy<string> DummyHash =
            new(() => BCrypt.Net.BCrypt.EnhancedHashPassword(Guid.NewGuid().ToString(), WorkFactor));

        public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);

        public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.EnhancedVerify(password, hash);

        public void VerifyDummy(string password) => BCrypt.Net.BCrypt.EnhancedVerify(password, DummyHash.Value);
    }
}
