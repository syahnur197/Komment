using Backend.Data;
using Backend.Entities;
using Backend.Features.Auth;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Sites;

public sealed class CreateSiteRequest
{
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;

    // Comma-separated, scheme + host + port, no trailing slash.
    public string Origins { get; set; } = default!;
}

public sealed class CreateSiteValidator : Validator<CreateSiteRequest>
{
    public CreateSiteValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Lowercase letters, digits and dashes only.");

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Origins)
            .NotEmpty().MaximumLength(1000)
            .Must(BeAbsoluteOrigins).WithMessage("Each origin must be an absolute URL with no path, e.g. https://blog.example.");
    }

    // The CORS allowlist is built from this, so a malformed entry silently
    // breaks every request from that blog.
    private static bool BeAbsoluteOrigins(string origins) =>
        origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(o => Uri.TryCreate(o, UriKind.Absolute, out var uri) &&
                      uri.GetLeftPart(UriPartial.Authority).Equals(o, StringComparison.OrdinalIgnoreCase));
}

public sealed class CreateSiteEndpoint : Endpoint<CreateSiteRequest, SiteResponse>
{
    public AppDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("");
        Group<SiteGroup>();
    }

    public override async Task HandleAsync(CreateSiteRequest req, CancellationToken ct)
    {
        if (await Db.Sites.AnyAsync(s => s.Slug == req.Slug, ct))
        {
            AddError(r => r.Slug, "That slug is taken.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var site = new Site
        {
            Slug = req.Slug,
            Name = req.Name,
            Origins = req.Origins,
            OwnerUserId = UserClaims.UserIdOf(User)!.Value,
        };

        Db.Sites.Add(site);
        await Db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<GetSiteByIdEndpoint>(
            new { Id = site.SiteId }, SiteResponse.From(site), cancellation: ct);
    }
}
