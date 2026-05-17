using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record AddLicenceMemberRequest(
    [Required]
    [EmailAddress]
    [StringLength(256)]
    string Email
);
