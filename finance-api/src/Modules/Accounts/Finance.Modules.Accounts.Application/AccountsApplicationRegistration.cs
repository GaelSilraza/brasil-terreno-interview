using Finance.Modules.Accounts.Application.Contracts;
using Finance.Modules.Accounts.Application.UseCases.GetAccount;
using Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;
using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Application.UseCases.OpenAccount;
using Finance.Modules.Accounts.Application.UseCases.UpdateAccountStatus;
using Finance.Modules.Accounts.Application.UseCases.UpdateOverdraftLimit;
using Finance.Modules.Accounts.Contracts;
using Finance.Shared.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Application;

public static class AccountsApplicationRegistration
{
    public static IServiceCollection AddAccountsApplication(this IServiceCollection services)
    {
        ValidationLanguage.UseEnglishMessages();
        services.AddValidatorsFromAssemblyContaining<OpenAccountValidator>(ServiceLifetime.Scoped);
        services.AddScoped<OpenAccountUseCase>();
        services.AddScoped<UpdateOverdraftLimitUseCase>();
        services.AddScoped<UpdateAccountStatusUseCase>();
        services.AddScoped<GetAccountUseCase>();
        services.AddScoped<ListAccountsUseCase>();
        services.AddScoped<ListAccountEntriesUseCase>();
        services.AddScoped<IAccountsModule, AccountsModule>();
        return services;
    }
}
