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
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
            var error = Error.Validation("Validation.Failed", errorMessage);

            // TResponse is Result<T>; use reflection to call Result.Failure<T>(error)
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureResult = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
                .MakeGenericMethod(valueType)
                .Invoke(null, [error])!;

            return (TResponse)failureResult;
        }

        return await next();

    }

}
