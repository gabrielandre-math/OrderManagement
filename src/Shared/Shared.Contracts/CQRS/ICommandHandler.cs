using MediatR;
using Shared.Contracts.Results;

namespace Shared.Contracts.CQRS;

public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Result>
    where TCommand : ICommand<Result>
{
}


public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse> 
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
}