using Microsoft.EntityFrameworkCore;

namespace Finance.Shared.Infrastructure.Persistence;

public interface IModuleDatabaseConfiguration
{
    int ApplyOrder { get; }

    void Configure(ModelBuilder modelBuilder);
}
