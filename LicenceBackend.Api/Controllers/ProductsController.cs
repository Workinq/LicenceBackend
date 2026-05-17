using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("products")]
[Authorize]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class ProductsController(
    IProductRepository products,
    IProductImageStorage images,
    TimeProvider time
) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private static readonly Dictionary<string, string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
    };
    private const long MaxImageBytes = 2 * 1024 * 1024;

    [HttpPost]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await products.FindBySlugAsync(request.Slug, cancellationToken);
        if (existing is not null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: ProblemTitles.SlugAlreadyExists,
                detail: $"A product with slug '{request.Slug}' already exists."
            );

        var product = new Product(
            Guid.NewGuid(),
            request.Slug,
            request.DisplayName,
            request.Description,
            request.Tagline,
            request.IsPublic ?? true,
            request.Price,
            request.Currency ?? "USD",
            request.SortOrder ?? 0,
            ImagePath: null,
            ImageContentType: null,
            time.GetUtcNow()
        );

        await products.CreateAsync(product, cancellationToken);

        var response = ToResponse(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var publicOnly = !User.IsInRole("admin");
        var page = await products.ListAsync(effectiveLimit, effectiveOffset, q, publicOnly, cancellationToken);
        var items = page.Items.Select(ToResponse).ToList();
        return Ok(new PagedResponse<ProductResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        var isAdmin = User.IsInRole("admin");
        if (product is null || (!isAdmin && !product.IsPublic))
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.ProductNotFound,
                detail: $"No product with id '{id}'."
            );

        return Ok(ToResponse(product));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.ProductNotFound,
                detail: $"No product with id '{id}'."
            );

        // PATCH semantics: a null field is left unchanged. Clearing description/tagline/price back to null is not supported here.
        var updated = product with
        {
            DisplayName = request.DisplayName ?? product.DisplayName,
            Description = request.Description ?? product.Description,
            Tagline = request.Tagline ?? product.Tagline,
            IsPublic = request.IsPublic ?? product.IsPublic,
            Price = request.Price ?? product.Price,
            Currency = request.Currency ?? product.Currency,
            SortOrder = request.SortOrder ?? product.SortOrder,
        };

        await products.UpdateAsync(updated, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpPost("{id:guid}/image")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(MaxImageBytes)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductNotFound, detail: $"No product with id '{id}'.");

        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidProductImage, detail: "No image file was provided.");
        if (file.Length > MaxImageBytes)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidProductImage, detail: "The image is larger than 2 MB.");
        if (!AllowedImageContentTypes.TryGetValue(file.ContentType, out var extension))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidProductImage, detail: "The image must be a PNG, JPEG, or WebP.");

        await using var stream = file.OpenReadStream();
        var storagePath = await images.SaveAsync(product.Id, extension, stream, cancellationToken);
        var updated = product with { ImagePath = storagePath, ImageContentType = file.ContentType };
        await products.UpdateAsync(updated, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product?.ImagePath is null || product.ImageContentType is null)
            return NotFound();
        var stream = await images.OpenReadAsync(product.ImagePath, cancellationToken);
        if (stream is null)
            return NotFound();
        return File(stream, product.ImageContentType);
    }

    [HttpDelete("{id:guid}/image")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductNotFound, detail: $"No product with id '{id}'.");
        if (product.ImagePath is not null)
            await images.DeleteAsync(product.ImagePath, cancellationToken);
        var updated = product with { ImagePath = null, ImageContentType = null };
        await products.UpdateAsync(updated, cancellationToken);
        return Ok(ToResponse(updated));
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Slug,
            product.DisplayName,
            product.Description,
            product.Tagline,
            product.IsPublic,
            product.Price,
            product.Currency,
            product.SortOrder,
            product.ImagePath is null ? null : $"/products/{product.Id}/image",
            product.CreatedAt);
    }
}
