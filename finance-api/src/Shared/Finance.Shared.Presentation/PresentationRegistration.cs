using Finance.Shared.Presentation.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Shared.Presentation;

public static class PresentationRegistration
{
    public static IServiceCollection AddFinancePresentation(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = FrameworkProblemDetails.AddMissingCode);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = actionContext =>
                ValidationProblemFactory.Create(ModelStateFieldErrors.From(actionContext.ModelState), "One or more validation errors occurred.");
        });
        return services;
    }
}
