using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceMembersTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Admin_can_add_list_and_remove_member()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var product = await CreateProductAsync("members-flow", "Members Flow");
        var ownerEmail = "owner-flow@test.local";
        var ownerId = await CreateUserAsync(ownerEmail, "owner-flow-pw-12345");
        var memberEmail = "member-flow@test.local";
        var memberId = await CreateUserAsync(memberEmail, "member-flow-pw-12345");

        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var listBefore = await AuthedClient.GetFromJsonAsync<MemberPayload[]>($"/licences/{licence.Id}/members");
        Assert.NotNull(listBefore);
        Assert.Empty(listBefore);

        var add = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var added = await add.Content.ReadFromJsonAsync<MemberPayload>();
        Assert.NotNull(added);
        Assert.Equal(memberId, added.UserId);
        Assert.Equal(memberEmail, added.Email);
        Assert.Equal(AdminUserId, added.AddedBy);
        Assert.Equal(AdminEmail, added.AddedByEmail);

        var listAfter = await AuthedClient.GetFromJsonAsync<MemberPayload[]>($"/licences/{licence.Id}/members");
        Assert.NotNull(listAfter);
        Assert.Single(listAfter);
        Assert.Equal(memberId, listAfter[0].UserId);

        var remove = await AuthedClient.DeleteAsync($"/licences/{licence.Id}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var listFinal = await AuthedClient.GetFromJsonAsync<MemberPayload[]>($"/licences/{licence.Id}/members");
        Assert.NotNull(listFinal);
        Assert.Empty(listFinal);
    }

    [SkippableFact]
    public async Task Add_member_with_unknown_email_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("unknown-email", "Unknown");
        var licence = await CreateLicenceAsync(product.Id, AdminUserId);

        var response = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = "nobody@test.local" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Add_member_who_is_owner_returns_409()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("owner-self", "Owner Self");
        var ownerEmail = "owner-self@test.local";
        var ownerId = await CreateUserAsync(ownerEmail, "owner-self-pw-12345");
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var response = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = ownerEmail });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [SkippableFact]
    public async Task Add_duplicate_member_returns_409()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dup-member", "Dup");
        var ownerId = await CreateUserAsync("dup-owner@test.local", "dup-owner-pw-12345");
        var memberEmail = "dup-member@test.local";
        await CreateUserAsync(memberEmail, "dup-member-pw-12345");
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var first = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [SkippableFact]
    public async Task Remove_unknown_member_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("missing-remove", "Missing");
        var licence = await CreateLicenceAsync(product.Id, AdminUserId);

        var response = await AuthedClient.DeleteAsync($"/licences/{licence.Id}/members/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task List_members_for_unknown_licence_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/licences/{Guid.NewGuid()}/members");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_can_add_and_remove_members_via_me_endpoints()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-members", "Me Members");
        var ownerEmail = "me-owner@test.local";
        var ownerPassword = "me-owner-pw-12345";
        var ownerId = await CreateUserAsync(ownerEmail, ownerPassword);
        var memberEmail = "me-member@test.local";
        var memberId = await CreateUserAsync(memberEmail, "me-member-pw-12345");
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);

        var add = await ownerClient.PostAsJsonAsync($"/me/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var added = await add.Content.ReadFromJsonAsync<MemberPayload>();
        Assert.NotNull(added);
        Assert.Equal(memberId, added.UserId);
        Assert.Equal(ownerId, added.AddedBy);

        var listed = await ownerClient.GetFromJsonAsync<MemberPayload[]>($"/me/licences/{licence.Id}/members");
        Assert.NotNull(listed);
        Assert.Single(listed);

        var remove = await ownerClient.DeleteAsync($"/me/licences/{licence.Id}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    }

    [SkippableFact]
    public async Task Non_owner_gets_404_for_me_member_management()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-not-owner", "Not Owner");
        var realOwnerEmail = "real-owner@test.local";
        var realOwnerId = await CreateUserAsync(realOwnerEmail, "real-owner-pw-12345");
        var licence = await CreateLicenceAsync(product.Id, realOwnerId);

        var strangerEmail = "stranger@test.local";
        var strangerPassword = "stranger-pw-12345";
        await CreateUserAsync(strangerEmail, strangerPassword);

        using var strangerClient = await CreateLoggedInClientAsync(strangerEmail, strangerPassword);
        var list = await strangerClient.GetAsync($"/me/licences/{licence.Id}/members");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        var add = await strangerClient.PostAsJsonAsync($"/me/licences/{licence.Id}/members", new { email = strangerEmail });
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
    }

    [SkippableFact]
    public async Task Member_cannot_manage_other_members_via_me_endpoints()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-member-only", "Member Only");
        var ownerEmail = "owner-mo@test.local";
        var ownerId = await CreateUserAsync(ownerEmail, "owner-mo-pw-12345");
        var memberEmail = "member-mo@test.local";
        var memberPassword = "member-mo-pw-12345";
        var memberId = await CreateUserAsync(memberEmail, memberPassword);
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var addAsAdmin = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, addAsAdmin.StatusCode);

        using var memberClient = await CreateLoggedInClientAsync(memberEmail, memberPassword);
        var list = await memberClient.GetAsync($"/me/licences/{licence.Id}/members");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        var remove = await memberClient.DeleteAsync($"/me/licences/{licence.Id}/members/{memberId}");
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [SkippableFact]
    public async Task Me_licences_returns_owned_and_shared_with_relationship_field()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-relationship", "Relationship");
        var aliceEmail = "alice-rel@test.local";
        var alicePassword = "alice-rel-pw-12345";
        var aliceId = await CreateUserAsync(aliceEmail, alicePassword);
        var bobEmail = "bob-rel@test.local";
        var bobPassword = "bob-rel-pw-12345";
        var bobId = await CreateUserAsync(bobEmail, bobPassword);

        var aliceLicence = await CreateLicenceAsync(product.Id, aliceId);
        var bobLicence = await CreateLicenceAsync(product.Id, bobId);

        var share = await AuthedClient.PostAsJsonAsync($"/licences/{bobLicence.Id}/members", new { email = aliceEmail });
        Assert.Equal(HttpStatusCode.Created, share.StatusCode);

        using var aliceClient = await CreateLoggedInClientAsync(aliceEmail, alicePassword);
        var mine = await aliceClient.GetFromJsonAsync<PagedMeLicencesPayload>("/me/licences");
        Assert.NotNull(mine);
        Assert.Equal(2, mine.Total);
        var ownedItem = mine.Items.Single(i => i.Id == aliceLicence.Id);
        Assert.Equal("owner", ownedItem.Relationship);
        var sharedItem = mine.Items.Single(i => i.Id == bobLicence.Id);
        Assert.Equal("member", sharedItem.Relationship);
    }

    [SkippableFact]
    public async Task Me_licences_id_serves_owner_and_member_but_404_for_strangers()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-licence-id", "By Id");
        var ownerEmail = "byid-owner@test.local";
        var ownerPassword = "byid-owner-pw-12345";
        var ownerId = await CreateUserAsync(ownerEmail, ownerPassword);
        var memberEmail = "byid-member@test.local";
        var memberPassword = "byid-member-pw-12345";
        await CreateUserAsync(memberEmail, memberPassword);
        var strangerEmail = "byid-stranger@test.local";
        var strangerPassword = "byid-stranger-pw-12345";
        await CreateUserAsync(strangerEmail, strangerPassword);

        var licence = await CreateLicenceAsync(product.Id, ownerId);
        await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);
        var ownerView = await ownerClient.GetFromJsonAsync<MeLicencePayload>($"/me/licences/{licence.Id}");
        Assert.NotNull(ownerView);
        Assert.Equal("owner", ownerView.Relationship);

        using var memberClient = await CreateLoggedInClientAsync(memberEmail, memberPassword);
        var memberView = await memberClient.GetFromJsonAsync<MeLicencePayload>($"/me/licences/{licence.Id}");
        Assert.NotNull(memberView);
        Assert.Equal("member", memberView.Relationship);

        using var strangerClient = await CreateLoggedInClientAsync(strangerEmail, strangerPassword);
        var strangerView = await strangerClient.GetAsync($"/me/licences/{licence.Id}");
        Assert.Equal(HttpStatusCode.NotFound, strangerView.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_get_user_licences_returns_owned_and_member_with_relationship()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("admin-user-lic", "Admin User Licences");
        var aliceEmail = "alice-admin@test.local";
        var aliceId = await CreateUserAsync(aliceEmail, "alice-admin-pw-12345");
        var bobEmail = "bob-admin@test.local";
        var bobId = await CreateUserAsync(bobEmail, "bob-admin-pw-12345");

        var aliceLicence = await CreateLicenceAsync(product.Id, aliceId);
        var bobLicence = await CreateLicenceAsync(product.Id, bobId);
        await AuthedClient.PostAsJsonAsync($"/licences/{bobLicence.Id}/members", new { email = aliceEmail });

        var page = await AuthedClient.GetFromJsonAsync<PagedMeLicencesPayload>($"/users/{aliceId}/licences");
        Assert.NotNull(page);
        Assert.Equal(2, page.Total);
        Assert.Equal("owner", page.Items.Single(i => i.Id == aliceLicence.Id).Relationship);
        Assert.Equal("member", page.Items.Single(i => i.Id == bobLicence.Id).Relationship);
    }

    [SkippableFact]
    public async Task Owner_regenerates_key_via_me_endpoint_and_old_key_stops_working()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-owner", "Regen Owner");
        var ownerEmail = "regen-owner@test.local";
        var ownerPassword = "regen-owner-pw-12345";
        var ownerId = await CreateUserAsync(ownerEmail, ownerPassword);
        var created = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = ownerId });
        var createdBody = await created.Content.ReadFromJsonAsync<RegenLicenceCreatedPayload>();
        Assert.NotNull(createdBody);
        var oldKey = createdBody.LicenceKey;

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);

        var regen = await ownerClient.PostAsJsonAsync($"/me/licences/{createdBody.Id}/regenerate-key", new { reason = "lost it" });
        Assert.Equal(HttpStatusCode.OK, regen.StatusCode);
        var regenBody = await regen.Content.ReadFromJsonAsync<RegenLicenceCreatedPayload>();
        Assert.NotNull(regenBody);
        Assert.False(string.IsNullOrWhiteSpace(regenBody.LicenceKey));
        Assert.NotEqual(oldKey, regenBody.LicenceKey);

        var oldVerify = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = oldKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, oldVerify.StatusCode);

        var newVerify = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = regenBody.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, newVerify.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_regenerate_on_revoked_licence_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-revoked", "Regen Revoked");
        var ownerEmail = "regen-revoked-owner@test.local";
        var ownerPassword = "regen-revoked-pw-12345";
        var ownerId = await CreateUserAsync(ownerEmail, ownerPassword);
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var revoke = await AuthedClient.PatchAsJsonAsync($"/licences/{licence.Id}/status", new { status = "revoked", reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);
        var response = await ownerClient.PostAsJsonAsync($"/me/licences/{licence.Id}/regenerate-key", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_regenerate_on_revoked_licence_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("admin-regen-revoked", "Admin Regen Revoked");
        var licence = await CreateLicenceAsync(product.Id, AdminUserId);

        var revoke = await AuthedClient.PatchAsJsonAsync($"/licences/{licence.Id}/status", new { status = "revoked", reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var response = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/regenerate-key", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_regenerate_on_suspended_licence_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-suspended", "Regen Suspended");
        var ownerEmail = "regen-suspended-owner@test.local";
        var ownerPassword = "regen-suspended-pw-12345";
        var ownerId = await CreateUserAsync(ownerEmail, ownerPassword);
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var suspend = await AuthedClient.PatchAsJsonAsync($"/licences/{licence.Id}/status", new { status = "suspended", reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);
        var response = await ownerClient.PostAsJsonAsync($"/me/licences/{licence.Id}/regenerate-key", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Non_owner_regenerate_via_me_endpoint_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-stranger", "Stranger Regen");
        var ownerId = await CreateUserAsync("regen-owner-x@test.local", "regen-owner-x-pw-12345");
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var strangerEmail = "regen-stranger@test.local";
        var strangerPassword = "regen-stranger-pw-12345";
        await CreateUserAsync(strangerEmail, strangerPassword);

        using var strangerClient = await CreateLoggedInClientAsync(strangerEmail, strangerPassword);
        var response = await strangerClient.PostAsJsonAsync($"/me/licences/{licence.Id}/regenerate-key", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Member_regenerate_via_me_endpoint_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-member", "Member Regen");
        var ownerId = await CreateUserAsync("regen-owner-m@test.local", "regen-owner-m-pw-12345");
        var memberEmail = "regen-member@test.local";
        var memberPassword = "regen-member-pw-12345";
        await CreateUserAsync(memberEmail, memberPassword);
        var licence = await CreateLicenceAsync(product.Id, ownerId);

        var addAsAdmin = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, addAsAdmin.StatusCode);

        using var memberClient = await CreateLoggedInClientAsync(memberEmail, memberPassword);
        var response = await memberClient.PostAsJsonAsync($"/me/licences/{licence.Id}/regenerate-key", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Adding_a_member_writes_an_audit_event()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("audit-add", "Audit");
        var memberEmail = "audit-member@test.local";
        await CreateUserAsync(memberEmail, "audit-pw-12345");
        var licence = await CreateLicenceAsync(product.Id, AdminUserId);

        var add = await AuthedClient.PostAsJsonAsync($"/licences/{licence.Id}/members", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var events = await AuthedClient.GetFromJsonAsync<PagedAuditPayload>(
                         $"/audit-events?subject_type=licence&subject_id={licence.Id}&event_type=licence.member_added");
        Assert.NotNull(events);
        Assert.True(events.Total >= 1);
        Assert.Contains(events.Items, e => e.EventType == "licence.member_added");
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body;
    }

    private async Task<Guid> CreateUserAsync(string email, string password)
    {
        var response = await AuthedClient.PostAsJsonAsync("/users", new { email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        return body.Id;
    }

    private async Task<LicencePayload> CreateLicenceAsync(Guid productId, Guid ownerUserId)
    {
        var response = await AuthedClient.PostAsJsonAsync("/licences", new { productId, userId = ownerUserId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(body);
        return body;
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record UserPayload(Guid Id, string Email, string? DisplayName, string Role, string Status, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string ProductSlug, Guid UserId, string UserEmail, string Status, DateTimeOffset CreatedAt);

    private sealed record MemberPayload(
        Guid UserId,
        string Email,
        string? DisplayName,
        Guid AddedBy,
        string? AddedByEmail,
        DateTimeOffset AddedAt
    );

    private sealed record AuditPayload(Guid Id, DateTimeOffset OccurredAt, string EventType, string SubjectType, Guid SubjectId, string ActorType, Guid? ActorUserId, string? ActorUserEmail, string? Reason);

    private sealed record PagedAuditPayload(IReadOnlyList<AuditPayload> Items, int Total, int Limit, int Offset);

    private sealed record MeLicencePayload(Guid Id, Guid ProductId, string ProductSlug, Guid UserId, string UserEmail, string Status, DateTimeOffset CreatedAt, string Relationship);

    private sealed record PagedMeLicencesPayload(IReadOnlyList<MeLicencePayload> Items, int Total, int Limit, int Offset);

    private sealed record RegenLicenceCreatedPayload(Guid Id, Guid ProductId, string ProductSlug, Guid UserId, string UserEmail, string Status, DateTimeOffset CreatedAt, string LicenceKey);
}
