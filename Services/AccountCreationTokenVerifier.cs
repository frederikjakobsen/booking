using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookingApp.Services
{

    public class AccountCreationTokenOptions
    {
        public string AcceptedHash { get; set; }
    }
    
    public class AccountCreationTokenVerifier
    {
        private readonly string _tokenHash;
        private readonly PasswordHasher<string> _hasher = new();
        private const string User = "Account Creation Token";

        public AccountCreationTokenVerifier(IOptions<AccountCreationTokenOptions> options)
        {
            _tokenHash = options.Value.AcceptedHash;
        }
        
        public bool VerifyToken(string token)
        {
            return _hasher.VerifyHashedPassword(User, _tokenHash, token) == PasswordVerificationResult.Success;
        }
    }
}