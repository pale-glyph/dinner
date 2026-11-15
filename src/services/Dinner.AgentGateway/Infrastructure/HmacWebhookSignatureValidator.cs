using System.Security.Cryptography;
using System.Text;
using Dinner.AgentGateway.Application;

namespace Dinner.AgentGateway.Infrastructure;

public sealed class HmacWebhookSignatureValidator : IWebhookSignatureValidator
{
    public bool IsValid(byte[] body, string secret, string signature)
    {
        const string Prefix = "sha256=";
        if (!signature.StartsWith(Prefix, StringComparison.Ordinal) || signature.Length != Prefix.Length + 64) return false;
        try
        {
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature[Prefix.Length..]));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
