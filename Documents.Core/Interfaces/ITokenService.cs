using Documents.Core.Entities;

namespace Documents.Core.Interfaces
{
    public interface ITokenService
    {
        /// <summary>JWT with sub, email, name, pwv claims.</summary>
        (string Token, DateTime ExpiresOn) CreateToken(UserEntity user);
    }
}
