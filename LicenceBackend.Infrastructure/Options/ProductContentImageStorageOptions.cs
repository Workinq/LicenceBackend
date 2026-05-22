namespace LicenceBackend.Infrastructure.Options;

public sealed class ProductContentImageStorageOptions
{
    public const string SectionName = "ProductContentImages";

    public string Directory { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "licencebackend-product-content-images");
}
