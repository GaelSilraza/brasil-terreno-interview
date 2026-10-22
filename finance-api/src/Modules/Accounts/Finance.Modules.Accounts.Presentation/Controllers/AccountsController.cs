using Finance.Modules.Accounts.Application.Responses;
using Finance.Modules.Accounts.Application.UseCases.GetAccount;
using Finance.Modules.Accounts.Application.UseCases.ListAccountEntries;
using Finance.Modules.Accounts.Application.UseCases.ListAccounts;
using Finance.Modules.Accounts.Application.UseCases.OpenAccount;
using Finance.Modules.Accounts.Application.UseCases.UpdateAccountStatus;
using Finance.Modules.Accounts.Application.UseCases.UpdateOverdraftLimit;
using Finance.Modules.Accounts.Presentation.Requests;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Presentation.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Modules.Accounts.Presentation.Controllers;

[Route("api/accounts")]
[Tags("Accounts")]
public sealed class AccountsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<CursorPage<AccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? search,
        [FromQuery] string? cursor,
        [FromQuery] string? direction,
        [FromQuery] int? limit,
        [FromServices] ListAccountsUseCase listAccountsUseCase,
        CancellationToken cancellationToken)
    {
        var accountsResult = await listAccountsUseCase.ExecuteAsync(new ListAccountsQuery(search, cursor, direction, limit), cancellationToken);
        return accountsResult.IsFailure ? ErrorResult(accountsResult.Failure) : Ok(accountsResult.Success);
    }

    [HttpGet("{accountId:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        Guid accountId,
        [FromServices] GetAccountUseCase getAccountUseCase,
        CancellationToken cancellationToken)
    {
        var accountResult = await getAccountUseCase.ExecuteAsync(accountId, cancellationToken);
        return accountResult.IsFailure ? ErrorResult(accountResult.Failure) : Ok(accountResult.Success);
    }

    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpenAsync(
        [FromBody] OpenAccountRequest request,
        [FromServices] OpenAccountUseCase openAccountUseCase,
        CancellationToken cancellationToken)
    {
        var command = new OpenAccountCommand(request.FullName, request.DocumentNumber, request.InitialBalance, request.OverdraftLimit);
        var accountResult = await openAccountUseCase.ExecuteAsync(command, cancellationToken);

        if (accountResult.IsFailure)
        {
            return ErrorResult(accountResult.Failure);
        }

        return Created($"/api/accounts/{accountResult.Success.Id}", accountResult.Success);
    }

    [HttpGet("{accountId:guid}/entries")]
    [ProducesResponseType<CursorPage<AccountEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEntriesAsync(
        Guid accountId,
        [FromQuery] string? cursor,
        [FromQuery] string? direction,
        [FromQuery] int? limit,
        [FromServices] ListAccountEntriesUseCase listAccountEntriesUseCase,
        CancellationToken cancellationToken)
    {
        var entriesResult = await listAccountEntriesUseCase.ExecuteAsync(new ListAccountEntriesQuery(accountId, cursor, direction, limit), cancellationToken);
        return entriesResult.IsFailure ? ErrorResult(entriesResult.Failure) : Ok(entriesResult.Success);
    }

    [HttpPut("{accountId:guid}/overdraft-limit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOverdraftLimitAsync(
        Guid accountId,
        [FromBody] UpdateOverdraftLimitRequest request,
        [FromServices] UpdateOverdraftLimitUseCase updateOverdraftLimitUseCase,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOverdraftLimitCommand(accountId, request.OverdraftLimit);
        var updateResult = await updateOverdraftLimitUseCase.ExecuteAsync(command, cancellationToken);
        return updateResult.IsFailure ? ErrorResult(updateResult.Failure) : NoContent();
    }

    [HttpPut("{accountId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatusAsync(
        Guid accountId,
        [FromBody] UpdateAccountStatusRequest request,
        [FromServices] UpdateAccountStatusUseCase updateAccountStatusUseCase,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAccountStatusCommand(accountId, request.Status);
        var updateResult = await updateAccountStatusUseCase.ExecuteAsync(command, cancellationToken);
        return updateResult.IsFailure ? ErrorResult(updateResult.Failure) : NoContent();
    }
}
