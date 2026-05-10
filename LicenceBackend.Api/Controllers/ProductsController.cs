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
[Authorize(Roles = "admin")]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class ProductsController(
    IProductRepository products,
    TimeProvider time
) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var page = await products.ListAsync(effectiveLimit, effectiveOffset, cancellationToken);
        var items = page.Items.Select(ToResponse).ToList();
        return Ok(new PagedResponse<ProductResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.ProductNotFound,
                detail: $"No product with id '{id}'."
            );

        return Ok(ToResponse(product));
    }

    private static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(product.Id, product.Slug, product.DisplayName, product.CreatedAt);
    }
}
