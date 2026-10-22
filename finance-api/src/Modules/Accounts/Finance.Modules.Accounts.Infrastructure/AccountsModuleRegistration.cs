using Finance.Modules.Accounts.Application;
using Finance.Modules.Accounts.Application.Queries;
using Finance.Modules.Accounts.Domain.Repositories;
using Finance.Modules.Accounts.Infrastructure.Persistence.Queries;
using Finance.Modules.Accounts.Infrastructure.Persistence.Repositories;
using Finance.Modules.Accounts.Infrastructure.Persistence.Seed;
using Finance.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Infrastructure;

public static class AccountsModuleRegistration
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleDatabaseConfiguration, AccountsDatabaseConfiguration>();
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountQueries, AccountQueries>();
        services.AddScoped<AccountsSeeder>();
        services.AddAccountsApplication();
        return services;
    }
}
