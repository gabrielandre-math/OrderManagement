using MediatR;
using Shared.Contracts.Results;

namespace Shared.Contracts.CQRS;

public interface ICommand : ICommand<Result>
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}
