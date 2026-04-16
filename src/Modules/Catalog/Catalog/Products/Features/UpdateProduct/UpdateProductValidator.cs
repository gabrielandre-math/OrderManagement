using Catalog.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Catalog.Products.Features.UpdateProduct;

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage(localizer["ProductIdRequired"]);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(localizer["ProductNameRequired"]);

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage(localizer["ProductPriceMustBePositive"]);
    }
}
