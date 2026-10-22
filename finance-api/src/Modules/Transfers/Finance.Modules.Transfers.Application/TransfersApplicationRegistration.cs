using Finance.Modules.Transfers.Application.DomainEventHandlers;
using Finance.Modules.Transfers.Application.Options;
using Finance.Modules.Transfers.Application.Services;
using Finance.Modules.Transfers.Application.UseCases.CancelTransfer;
using Finance.Modules.Transfers.Application.UseCases.CreateImmediateTransfer;
using Finance.Modules.Transfers.Application.UseCases.DispatchDueScheduledTransfers;
using Finance.Modules.Transfers.Application.UseCases.ExecuteScheduledTransfer;
using Finance.Modules.Transfers.Application.UseCases.GetTransfer;
using Finance.Modules.Transfers.Application.UseCases.GetTransferLimits;
using Finance.Modules.Transfers.Application.UseCases.ListAccountTransfers;
using Finance.Modules.Transfers.Application.UseCases.ScheduleTransfer;
using Finance.Modules.Transfers.Application.UseCases.SetTransferLimits;
using Finance.Modules.Transfers.Domain.Services;
using Finance.Shared.Application.DomainEvents;
using Finance.Shared.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Finance.Modules.Transfers.Application;

public static class TransfersApplicationRegistration
{
    public static IServiceCollection AddTransfersApplication(this IServiceCollection services)
    {
        ValidationLanguage.UseEnglishMessages();
        services.AddValidatorsFromAssemblyContaining<CreateImmediateTransferValidator>(ServiceLifetime.Scoped);
        services.AddSingleton(serviceProvider =>
        {
            var transferLimitsOptions = serviceProvider.GetRequiredService<IOptions<TransferLimitsOptions>>().Value;
            return new TransferPeriodSchedule(
                transferLimitsOptions.TimeZoneId,
                transferLimitsOptions.DaytimeStartsAt,
                transferLimitsOptions.NighttimeStartsAt);
        });
        services.AddScoped<TransferLimitPolicyResolver>();
        services.AddScoped<TransferExecutor>();
        services.AddScoped<TransferResponseFactory>();
        services.AddScoped<IdempotentTransferReplay>();
        services.AddScoped<CreateImmediateTransferUseCase>();
        services.AddScoped<ScheduleTransferUseCase>();
        services.AddScoped<CancelTransferUseCase>();
        services.AddScoped<GetTransferUseCase>();
        services.AddScoped<ListAccountTransfersUseCase>();
        services.AddScoped<GetTransferLimitsUseCase>();
        services.AddScoped<SetTransferLimitsUseCase>();
        services.AddScoped<DispatchDueScheduledTransfersUseCase>();
        services.AddScoped<ExecuteScheduledTransferUseCase>();
        services.AddScoped<IDomainEventHandler, TransferScheduledDomainEventHandler>();
        services.AddScoped<IDomainEventHandler, TransferProcessingStartedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler, TransferCompletedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler, TransferFailedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler, TransferCancelledDomainEventHandler>();
        return services;
    }
}
