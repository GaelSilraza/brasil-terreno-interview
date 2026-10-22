using System.Text.RegularExpressions;

namespace Finance.Modules.Accounts.Domain.ValueObjects;

public static partial class DocumentNumberFormat
{
    public static bool IsAccepted(string? documentNumber)
    {
        if (documentNumber is null)
        {
            return false;
        }

        var trimmedDocumentNumber = documentNumber.Trim();
        return DigitsOnlyPattern().IsMatch(trimmedDocumentNumber) || MaskedPattern().IsMatch(trimmedDocumentNumber);
    }

    public static string Normalize(string documentNumber)
    {
        return new string(documentNumber.Where(char.IsAsciiDigit).ToArray());
    }

    [GeneratedRegex(@"^\d{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsOnlyPattern();

    [GeneratedRegex(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex MaskedPattern();
}
