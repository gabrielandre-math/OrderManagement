using Microsoft.Extensions.Localization;

namespace Shared.Contracts.Results;

public static class ErrorExtensions 
{
    public static Error ToLocalized(this Error error, IStringLocalizer localizer)
        => error with { Message = localizer[error.Code] };
}