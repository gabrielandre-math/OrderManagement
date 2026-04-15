using Catalog.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
    }
}
