namespace LicenceBackend.Infrastructure.Options;

public sealed class ProductFileStorageOptions
{
    public const string SectionName = "ProductFiles";

    public string Directory { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "licencebackend-product-files");

    public long MaxFileBytes { get; set; } = 200L * 1024 * 1024;
}
