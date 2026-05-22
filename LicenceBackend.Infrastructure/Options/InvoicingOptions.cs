using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Infrastructure.Options;

public sealed class InvoicingOptions
{
    public const string SectionName = "Invoicing";

    public string SellerName { get; init; } = "";
    public string SellerAddressLine1 { get; init; } = "";
    public string SellerAddressLine2 { get; init; } = "";
    public string SellerCity { get; init; } = "";
    public string SellerRegion { get; init; } = "";
    public string SellerPostalCode { get; init; } = "";
    public string SellerCountry { get; init; } = "";

    public string NumberPrefix { get; init; } = "INV-";

    [Range(1, 12)] public int NumberPadWidth { get; init; } = 5;

    public string FormatNumber(long invoiceNumber) =>
        NumberPrefix + invoiceNumber.ToString().PadLeft(NumberPadWidth, '0');
}
