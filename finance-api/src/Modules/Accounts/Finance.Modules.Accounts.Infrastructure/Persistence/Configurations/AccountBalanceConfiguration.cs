using Finance.Modules.Accounts.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Modules.Accounts.Infrastructure.Persistence.Configurations;

public sealed class AccountBalanceConfiguration : IEntityTypeConfiguration<AccountBalance>
{
    public void Configure(EntityTypeBuilder<AccountBalance> builder)
    {
        builder.ToView(AccountBalance.ViewName);
        builder.HasNoKey();
        builder.Property(accountBalance => accountBalance.Balance).HasPrecision(18, 2);
    }
}
