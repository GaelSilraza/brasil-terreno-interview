using System.Text.Json;

namespace Finance.Shared.Application.Validation;

public static class FieldPath
{
    public static string ToCamelCase(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
        {
            return string.Empty;
        }

        var segments = propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment));
        return string.Join('.', segments);
    }
}
