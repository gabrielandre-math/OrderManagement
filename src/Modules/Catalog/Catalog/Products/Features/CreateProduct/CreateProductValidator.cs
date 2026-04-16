using Catalog.Resources;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Catalog.Products.Features.CreateProduct;

internal class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator(IStringLocalizer<CatalogMessages> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage(localizer["ProductNameRequired"])
            .MaximumLength(100)
            .WithMessage(localizer["ProductNameMaxLength"]);

        RuleFor(x => x.Price).GreaterThan(0)
            .WithMessage(localizer["ProductPriceMustBePositive"]);
    }
}
