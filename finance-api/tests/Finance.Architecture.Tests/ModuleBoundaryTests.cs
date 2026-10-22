using System.Reflection;

namespace Finance.Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    [Theory]
    [InlineData("Finance.Shared.Domain")]
    [InlineData("Finance.Shared.Application")]
    [InlineData("Finance.Shared.Infrastructure")]
    [InlineData("Finance.Shared.Presentation")]
    public void SharedAssemblies_DoNotReferenceAnyModule(string assemblyName)
    {
        var financeReferences = FinanceReferencesOf(assemblyName);

        Assert.DoesNotContain(financeReferences, referencedAssemblyName => referencedAssemblyName.StartsWith("Finance.Modules.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Finance.Modules.Accounts.Domain")]
    [InlineData("Finance.Modules.Accounts.Contracts")]
    [InlineData("Finance.Modules.Accounts.Application")]
    [InlineData("Finance.Modules.Accounts.Infrastructure")]
    [InlineData("Finance.Modules.Accounts.Presentation")]
    public void AccountsAssemblies_DoNotReferenceTransfers(string assemblyName)
    {
        var financeReferences = FinanceReferencesOf(assemblyName);

        Assert.DoesNotContain(financeReferences, referencedAssemblyName => referencedAssemblyName.StartsWith("Finance.Modules.Transfers.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Finance.Modules.Transfers.Domain")]
    [InlineData("Finance.Modules.Transfers.Application")]
    [InlineData("Finance.Modules.Transfers.Infrastructure")]
    [InlineData("Finance.Modules.Transfers.Presentation")]
    public void TransfersAssemblies_ReferenceAccountsOnlyThroughContracts(string assemblyName)
    {
        var accountsReferences = FinanceReferencesOf(assemblyName)
            .Where(referencedAssemblyName => referencedAssemblyName.StartsWith("Finance.Modules.Accounts.", StringComparison.Ordinal))
            .ToList();

        Assert.All(accountsReferences, referencedAssemblyName => Assert.Equal("Finance.Modules.Accounts.Contracts", referencedAssemblyName));
    }

    [Theory]
    [InlineData("Finance.Modules.Accounts.Domain")]
    [InlineData("Finance.Modules.Transfers.Domain")]
    public void DomainAssemblies_ReferenceOnlySharedDomain(string assemblyName)
    {
        var financeReferences = FinanceReferencesOf(assemblyName);

        Assert.All(financeReferences, referencedAssemblyName => Assert.Equal("Finance.Shared.Domain", referencedAssemblyName));
    }

    [Theory]
    [InlineData("Finance.Shared.Domain")]
    [InlineData("Finance.Shared.Application")]
    [InlineData("Finance.Modules.Accounts.Domain")]
    [InlineData("Finance.Modules.Accounts.Contracts")]
    [InlineData("Finance.Modules.Accounts.Application")]
    [InlineData("Finance.Modules.Transfers.Domain")]
    [InlineData("Finance.Modules.Transfers.Application")]
    public void ApplicationAssemblies_DoNotReferenceEntityFrameworkOrAspNetCore(string assemblyName)
    {
        string[] forbiddenPrefixes = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql"];

        var forbiddenReferences = AllReferencesOf(assemblyName)
            .Where(referencedAssemblyName => forbiddenPrefixes.Any(forbiddenPrefix => referencedAssemblyName.StartsWith(forbiddenPrefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbiddenReferences);
    }

    [Theory]
    [InlineData("Finance.Shared.Presentation")]
    [InlineData("Finance.Modules.Accounts.Presentation")]
    [InlineData("Finance.Modules.Transfers.Presentation")]
    public void PresentationAssemblies_DoNotReferenceInfrastructure(string assemblyName)
    {
        var financeReferences = FinanceReferencesOf(assemblyName);

        Assert.DoesNotContain(financeReferences, referencedAssemblyName => referencedAssemblyName.EndsWith(".Infrastructure", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Finance.Modules.Accounts.Application")]
    [InlineData("Finance.Modules.Transfers.Application")]
    public void UseCaseClasses_AreSealedAndExposeExecuteAsync(string assemblyName)
    {
        var useCaseTypes = Assembly.Load(new AssemblyName(assemblyName))
            .GetTypes()
            .Where(type => type.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(useCaseTypes);
        Assert.All(useCaseTypes, useCaseType =>
        {
            Assert.True(useCaseType.IsClass, $"{useCaseType.FullName} must be a class.");
            Assert.True(useCaseType.IsSealed, $"{useCaseType.FullName} must be sealed.");
            Assert.NotNull(useCaseType.GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Instance));
        });
    }

    private static IReadOnlyList<string> FinanceReferencesOf(string assemblyName)
    {
        return Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(referencedAssembly => referencedAssembly.Name ?? string.Empty)
            .Where(referencedAssemblyName => referencedAssemblyName.StartsWith("Finance.", StringComparison.Ordinal))
            .ToList();
    }

    private static IReadOnlyList<string> AllReferencesOf(string assemblyName)
    {
        return Assembly.Load(new AssemblyName(assemblyName))
            .GetReferencedAssemblies()
            .Select(referencedAssembly => referencedAssembly.Name ?? string.Empty)
            .ToList();
    }
}
