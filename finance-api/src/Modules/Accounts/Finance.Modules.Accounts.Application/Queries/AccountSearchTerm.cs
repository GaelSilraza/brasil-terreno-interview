using System.Text.RegularExpressions;

namespace Finance.Modules.Accounts.Application.Queries;

public sealed partial record AccountSearchTerm(AccountSearchKind Kind, string Value)
{
    public const int MaximumLength = 100;

    public static AccountSearchTerm Parse(string? search)
    {
        var trimmedSearch = search?.Trim() ?? string.Empty;

        if (trimmedSearch.Length == 0)
        {
            return new AccountSearchTerm(AccountSearchKind.All, string.Empty);
        }

        if (DocumentLikePattern().IsMatch(trimmedSearch))
        {
            var digits = new string(trimmedSearch.Where(char.IsAsciiDigit).ToArray());

            if (digits.Length is >= 1 and <= 11)
            {
                return new AccountSearchTerm(AccountSearchKind.DocumentNumberPrefix, digits);
            }
        }

        return new AccountSearchTerm(AccountSearchKind.HolderName, trimmedSearch);
    }

    [GeneratedRegex(@"^[\d.\-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentLikePattern();
}
