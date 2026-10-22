using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Application.UseCases.OpenAccount;
using Finance.Modules.Accounts.Domain.Entities;
using Finance.Shared.Domain;
using Finance.Shared.Infrastructure.Persistence;
using Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Modules.Accounts.Tests;

public sealed class DocumentNumberTests(AccountsTestFixture fixture) : IAsyncLifetime
{
    private readonly FinanceTestEnvironment environment = fixture.CreateEnvironment();

    public async ValueTask InitializeAsync()
    {
        await environment.ResetDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await environment.DisposeAsync();
    }

    [Fact]
    public async Task OpeningAccount_WithMaskedDocument_StoresOnlyDigits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", "123.456.789-01", 250m, 300m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.True(accountResult.IsSuccessful);
        Assert.Equal("12345678901", accountResult.Success.DocumentNumber);
        var storedDocumentNumbers = await GetStoredDocumentNumbersAsync(cancellationToken);
        Assert.Equal(["12345678901"], storedDocumentNumbers);
    }

    [Fact]
    public async Task OpeningAccount_WithDigitsOnlyDocument_StoresTheSameDigits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", "12345678901", 250m, 300m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.True(accountResult.IsSuccessful);
        Assert.Equal("12345678901", accountResult.Success.DocumentNumber);
        var storedDocumentNumbers = await GetStoredDocumentNumbersAsync(cancellationToken);
        Assert.Equal(["12345678901"], storedDocumentNumbers);
    }

    [Fact]
    public async Task OpeningAccount_WithMaskedDocumentOfExistingPerson_ReturnsDocumentNumberAlreadyExists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstCommand = new OpenAccountCommand("Fernanda Alves", "12345678901", 250m, 300m);
        var secondCommand = new OpenAccountCommand("Gustavo Prado", "123.456.789-01", 100m, 0m);
        await OpenAccountAsync(firstCommand, cancellationToken);

        var secondResult = await OpenAccountAsync(secondCommand, cancellationToken);

        Assert.True(secondResult.IsFailure);
        Assert.Equal("DocumentNumberAlreadyExists", secondResult.Failure.Code);
        var storedDocumentNumbers = await GetStoredDocumentNumbersAsync(cancellationToken);
        Assert.Equal(["12345678901"], storedDocumentNumbers);
    }

    [Theory]
    [InlineData("123.45678901")]
    [InlineData("1234567890")]
    [InlineData("123.456.789/01")]
    [InlineData("abc.def.ghi-jk")]
    [InlineData("123456789012")]
    public async Task OpeningAccount_WithMalformedDocument_ReturnsInvalidFormat(string documentNumber)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new OpenAccountCommand("Fernanda Alves", documentNumber, 250m, 300m);

        var accountResult = await OpenAccountAsync(command, cancellationToken);

        Assert.True(accountResult.IsFailure);
        Assert.Equal("ValidationFailed", accountResult.Failure.Code);
        var fieldError = Assert.Single(accountResult.Failure.FieldErrors);
        Assert.Equal("documentNumber", fieldError.Field);
        Assert.Equal(FieldErrorCodes.InvalidFormat, fieldError.Code);
        var storedDocumentNumbers = await GetStoredDocumentNumbersAsync(cancellationToken);
        Assert.Empty(storedDocumentNumbers);
    }

    private async Task<Either<Error, AccountResponse>> OpenAccountAsync(
        OpenAccountCommand command,
        CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var openAccountUseCase = scope.ServiceProvider.GetRequiredService<OpenAccountUseCase>();
        return await openAccountUseCase.ExecuteAsync(command, cancellationToken);
    }

    private async Task<List<string>> GetStoredDocumentNumbersAsync(CancellationToken cancellationToken)
    {
        await using var scope = environment.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        return await dbContext.Set<Person>()
            .AsNoTracking()
            .Select(person => person.DocumentNumber)
            .ToListAsync(cancellationToken);
    }
}
