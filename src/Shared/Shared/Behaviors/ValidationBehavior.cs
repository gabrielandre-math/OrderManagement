using FluentValidation;
using MediatR;
using Shared.Contracts.CQRS;
using Shared.Contracts.Results;

namespace Shared.Behaviors;

public class ValidationBehavior<TRequest, TResponse>
    (IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults.Where(r => r.Errors.Any()).SelectMany(r => r.Errors).ToList();

        if (failures.Count > 0)
        {
            // Concatenates all validation messages (already located by FluentValidation)
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));

            // Creates a Result.Failure instead of throwing an exception
            // The TResponse can be Result or Result<T> — both inherit from Result
            return (dynamic)Result.Failure(
                Error.Validation("Validation.Failed", errorMessage));
        }

        return await next();

    }

}
