using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Finance.Shared.Infrastructure.Persistence;

public sealed class ModuleModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is FinanceDbContext financeDbContext)
        {
            return (financeDbContext.ModelCacheKey, designTime);
        }

        return (context.GetType(), designTime);
    }
}
