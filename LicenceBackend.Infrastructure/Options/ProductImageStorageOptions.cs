namespace LicenceBackend.Infrastructure.Options;

public sealed class ProductImageStorageOptions
{
    public const string SectionName = "ProductImages";

    public string Directory { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "licencebackend-product-images");
}
