using System.Globalization;
using FluentValidation;

namespace Finance.Shared.Application.Validation;

public static class ValidationLanguage
{
    public static void UseEnglishMessages()
    {
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");
    }
}
