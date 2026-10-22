using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.UseCases.CancelTransfer;
using Finance.Modules.Transfers.Application.UseCases.CreateImmediateTransfer;
using Finance.Modules.Transfers.Application.UseCases.GetTransfer;
using Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;
using Finance.Modules.Transfers.Application.UseCases.ScheduleTransfer;
using Finance.Modules.Transfers.Presentation.Requests;
using Finance.Shared.Application.Pagination;
using Finance.Shared.Domain;
using Finance.Shared.Presentation.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Modules.Transfers.Presentation.Controllers;

[Route("api/transfers")]
[Tags("Transfers")]
public sealed class TransfersController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateTransferRequest request,
        [FromHeader(Name = IdempotencyHeaders.IdempotencyKey)] string? idempotencyKey,
        [FromServices] CreateImmediateTransferUseCase createImmediateTransferUseCase,
        CancellationToken cancellationToken)
    {
        var command = new CreateImmediateTransferCommand(request.SourceAccountId, request.DestinationAccountId, request.Amount, idempotencyKey);
        var transferResult = await createImmediateTransferUseCase.ExecuteAsync(command, cancellationToken);
        return ToCreatedResult(transferResult);
    }

    [HttpPost("scheduled")]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ScheduleAsync(
        [FromBody] ScheduleTransferRequest request,
        [FromHeader(Name = IdempotencyHeaders.IdempotencyKey)] string? idempotencyKey,
        [FromServices] ScheduleTransferUseCase scheduleTransferUseCase,
        CancellationToken cancellationToken)
    {
        var command = new ScheduleTransferCommand(request.SourceAccountId, request.DestinationAccountId, request.Amount, request.ScheduledFor, idempotencyKey);
        var transferResult = await scheduleTransferUseCase.ExecuteAsync(command, cancellationToken);
        return ToCreatedResult(transferResult);
    }

    [HttpGet]
    [ProducesResponseType<CursorPage<TransferResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? accountId,
        [FromQuery] string? cursor,
        [FromQuery] string? direction,
        [FromQuery] int? limit,
        [FromServices] ListAccountTransfersUseCase listAccountTransfersUseCase,
        CancellationToken cancellationToken)
    {
        var transfersResult = await listAccountTransfersUseCase.ExecuteAsync(new ListAccountTransfersQuery(accountId, cursor, direction, limit), cancellationToken);
        return transfersResult.IsFailure ? ErrorResult(transfersResult.Failure) : Ok(transfersResult.Success);
    }

    [HttpGet("{transferId:guid}")]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        Guid transferId,
        [FromServices] GetTransferUseCase getTransferUseCase,
        CancellationToken cancellationToken)
    {
        var transferResult = await getTransferUseCase.ExecuteAsync(transferId, cancellationToken);
        return transferResult.IsFailure ? ErrorResult(transferResult.Failure) : Ok(transferResult.Success);
    }

    [HttpPost("{transferId:guid}/cancel")]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(
        Guid transferId,
        [FromServices] CancelTransferUseCase cancelTransferUseCase,
        CancellationToken cancellationToken)
    {
        var cancellationResult = await cancelTransferUseCase.ExecuteAsync(transferId, cancellationToken);
        return cancellationResult.IsFailure ? ErrorResult(cancellationResult.Failure) : Ok(cancellationResult.Success);
    }

    private IActionResult ToCreatedResult(Either<Error, TransferResult> transferResult)
    {
        if (transferResult.IsFailure)
        {
            return ErrorResult(transferResult.Failure);
        }

        if (transferResult.Success.IsIdempotentReplay)
        {
            MarkAsIdempotentReplay();
        }

        var transfer = transferResult.Success.Transfer;
        return Created($"/api/transfers/{transfer.Id}", transfer);
    }
}
