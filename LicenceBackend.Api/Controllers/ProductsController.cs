using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
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
    IProductFileRepository productFiles,
    IProductFileStorage productFileStorage,
    IProductContentImageStorage contentImages,
    IProductContentImageRepository contentImageRepository,
    IAuditEventRepository auditEvents,
    Microsoft.Extensions.Options.IOptions<LicenceBackend.Infrastructure.Options.ProductFileStorageOptions> productFileOptions,
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
    private const long MaxUploadRequestBytes = 200L * 1024 * 1024;
    private const int MaxPageContentBytes = 256 * 1024;

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
        var items = page.Items.Select(p => ToResponse(p, includePageContent: false)).ToList();
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

        if (request.PageContent is { } pageContent)
        {
            var pageContentBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(pageContent));
            if (pageContentBytes > MaxPageContentBytes)
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.PageContentTooLarge,
                    detail: $"Page content exceeds the {MaxPageContentBytes} byte limit.");
        }

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
            PageContent = request.PageContent ?? product.PageContent,
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

    [HttpPost("{id:guid}/files")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [ProducesResponseType(typeof(ProductFileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadFile(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductNotFound, detail: $"No product with id '{id}'.");

        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.ProductFileEmpty, detail: "No file was provided.");

        var maxBytes = productFileOptions.Value.MaxFileBytes;
        if (file.Length > maxBytes)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.ProductFileTooLarge, detail: $"The file is larger than {maxBytes} bytes.");

        if (!TryGetCurrentUserId(out var adminId)) return Unauthorized();

        var fileId = Guid.NewGuid();
        await using (var stream = file.OpenReadStream())
        {
            await productFileStorage.SaveAsync(fileId, stream, cancellationToken);
        }
        var storagePath = fileId.ToString();

        var versionNumber = await productFiles.GetNextVersionNumberAsync(product.Id, cancellationToken);
        var now = time.GetUtcNow();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        var fileName = string.IsNullOrWhiteSpace(file.FileName) ? "file" : file.FileName;

        var productFile = new ProductFile(
            fileId,
            product.Id,
            versionNumber,
            fileName,
            storagePath,
            contentType,
            file.Length,
            adminId,
            now);
        await productFiles.CreateAsync(productFile, cancellationToken);

        var evt = AuditEvent.Create(
            AuditEventTypes.ProductFileUploaded,
            AuditSubjectTypes.Product,
            product.Id,
            AuditActorTypes.Admin,
            adminId,
            reason: null,
            new ProductFileUploadedPayload(productFile.Id, versionNumber, fileName, contentType, file.Length),
            now);
        await auditEvents.RecordAsync(evt, cancellationToken);

        var response = ToFileResponse(productFile);
        return CreatedAtAction(nameof(GetFile), new { id = product.Id, fileId = productFile.Id }, response);
    }

    [HttpGet("{id:guid}/files")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductFileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListFiles(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductNotFound, detail: $"No product with id '{id}'.");
        var files = await productFiles.ListByProductAsync(id, cancellationToken);
        return Ok(files.Select(ToFileResponse).ToList());
    }

    [HttpGet("{id:guid}/files/{fileId:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(ProductFileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(Guid id, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await productFiles.FindByIdAsync(fileId, cancellationToken);
        if (file is null || file.ProductId != id)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductFileNotFound, detail: $"No file with id '{fileId}' on product '{id}'.");
        return Ok(ToFileResponse(file));
    }

    [HttpGet("{id:guid}/files/{fileId:guid}/download")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(Guid id, Guid fileId, CancellationToken cancellationToken)
    {
        var file = await productFiles.FindByIdAsync(fileId, cancellationToken);
        if (file is null || file.ProductId != id)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductFileNotFound, detail: $"No file with id '{fileId}' on product '{id}'.");
        var stream = await productFileStorage.OpenReadAsync(file.StoragePath, cancellationToken);
        if (stream is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductFileNotFound, detail: $"File blob for '{fileId}' is missing.");
        return File(stream, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/content-images")]
    [Authorize(Roles = "admin")]
    [RequestSizeLimit(MaxImageBytes)]
    [ProducesResponseType(typeof(ProductContentImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadContentImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        var product = await products.FindByIdAsync(id, cancellationToken);
        if (product is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductNotFound, detail: $"No product with id '{id}'.");

        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidProductContentImage, detail: "No image file was provided.");
        if (file.Length > MaxImageBytes)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidProductContentImage, detail: "The image is larger than 2 MB.");
        if (!AllowedImageContentTypes.TryGetValue(file.ContentType, out var extension))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidProductContentImage, detail: "The image must be a PNG, JPEG, or WebP.");

        if (!TryGetCurrentUserId(out var adminId)) return Unauthorized();

        var imageId = Guid.NewGuid();
        string storagePath;
        await using (var stream = file.OpenReadStream())
        {
            storagePath = await contentImages.SaveAsync(imageId, extension, stream, cancellationToken);
        }

        var image = new ProductContentImage(
            imageId,
            product.Id,
            storagePath,
            file.ContentType,
            file.Length,
            adminId,
            time.GetUtcNow());
        await contentImageRepository.CreateAsync(image, cancellationToken);

        var response = new ProductContentImageResponse(image.Id, $"/products/{product.Id}/content-images/{image.Id}");
        return CreatedAtAction(nameof(GetContentImage), new { id = product.Id, imageId = image.Id }, response);
    }

    [HttpGet("{id:guid}/content-images/{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await contentImageRepository.FindByIdAsync(imageId, cancellationToken);
        if (image is null || image.ProductId != id)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductContentImageNotFound, detail: $"No content image '{imageId}' on product '{id}'.");

        var product = await products.FindByIdAsync(id, cancellationToken);
        var isAdmin = User.IsInRole("admin");
        if (product is null || (!isAdmin && !product.IsPublic))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductContentImageNotFound, detail: $"No content image '{imageId}' on product '{id}'.");

        var stream = await contentImages.OpenReadAsync(image.StoragePath, cancellationToken);
        if (stream is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductContentImageNotFound, detail: $"Image blob for '{imageId}' is missing.");
        return File(stream, image.ContentType);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }

    private static ProductFileResponse ToFileResponse(ProductFile file)
    {
        return new ProductFileResponse(
            file.Id,
            file.ProductId,
            file.VersionNumber,
            file.FileName,
            file.ContentType,
            file.FileSizeBytes,
            file.UploadedByAdminId,
            file.UploadedAt);
    }

    private static ProductResponse ToResponse(Product product, bool includePageContent = true)
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
            product.CreatedAt,
            includePageContent ? product.PageContent : null);
    }
}
