using Finance.Modules.Transfers.Application.Responses;
using Finance.Modules.Transfers.Application.UseCases.GetTransferLimits;
using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Modules.Transfers.Presentation.Requests;
using Finance.Shared.Presentation.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Modules.Transfers.Presentation.Controllers;

[Route("api/transfer-limits")]
[Tags("TransferLimits")]
public sealed class TransferLimitsController : ApiControllerBase
{
    [HttpGet("{accountId:guid}")]
    [ProducesResponseType<TransferLimitsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        Guid accountId,
        [FromServices] GetTransferLimitsUseCase getTransferLimitsUseCase,
        CancellationToken cancellationToken)
    {
        var transferLimitsResult = await getTransferLimitsUseCase.ExecuteAsync(accountId, cancellationToken);
        return transferLimitsResult.IsFailure ? ErrorResult(transferLimitsResult.Failure) : Ok(transferLimitsResult.Success);
    }

    [HttpPut("{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAsync(
        Guid accountId,
        [FromBody] SetTransferLimitsRequest request,
        [FromServices] SetTransferLimitsUseCase setTransferLimitsUseCase,
        CancellationToken cancellationToken)
    {
        var command = new SetTransferLimitsCommand(
            accountId,
            request.DaytimeMaxAmountPerHour,
            request.DaytimeMaxAttemptsPerHour,
            request.NighttimeMaxAmountPerHour,
            request.NighttimeMaxAttemptsPerHour);
        var setResult = await setTransferLimitsUseCase.ExecuteAsync(command, cancellationToken);
        return setResult.IsFailure ? ErrorResult(setResult.Failure) : NoContent();
    }
}
