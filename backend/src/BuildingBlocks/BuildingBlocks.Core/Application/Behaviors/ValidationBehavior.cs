using FluentValidation;

namespace BuildingBlocks.Application;

/// <summary>Runs all FluentValidation validators for the request before the handler.
/// On failure throws ValidationException → mapped to 400 ProblemDetails by the web layer.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var failures = new List<FluentValidation.Results.ValidationFailure>();
            foreach (var validator in validators)
            {
                // Fresh context per validator: a shared ValidationContext accumulates failures, so each later
                // result would re-report the earlier validators' errors (duplicates in the 400 response).
                var result = await validator.ValidateAsync(request, cancellationToken);
                if (!result.IsValid)
                    failures.AddRange(result.Errors);
            }

            if (failures.Count > 0)
                throw new ValidationException(failures);
        }

        return await next(request, cancellationToken);
    }
}
