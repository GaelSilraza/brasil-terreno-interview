using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Finance.Modules.Transfers.Domain.Enums;

namespace Finance.Modules.Transfers.Application.Services;

public static class TransferRequestFingerprint
{
    public static string Compute(
        TransferType type,
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        DateTimeOffset? scheduledFor)
    {
        var canonicalRequest = string.Join(
            '|',
            type.ToString(),
            sourceAccountId.ToString("N"),
            destinationAccountId.ToString("N"),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            scheduledFor?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
    }
}
