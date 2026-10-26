using System.Globalization;
using System.Security.Claims;
using System.Text;
using DKH.CustomerService.Api.Journal;
using DKH.CustomerService.Application.DeliveryContacts;

namespace DKH.CustomerService.Api.DeliveryContacts;

// Request-local observation of the already validated personal caller. These
// bytes never leave request memory and do not grant contact access.
internal sealed class DeliveryContactCallerFence
{
    private readonly HttpContext _context;
    private readonly Func<HttpContext?> _currentContext;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly ClaimsPrincipal _principal;
    private readonly System.Security.Principal.IIdentity? _identity;
    private readonly byte[] _principalBytes;
    private readonly string?[] _authorization;
    private readonly string?[] _workspaces;
    private readonly string?[] _storefronts;
    private readonly PathString _path;

    public DeliveryContactCallerFence(HttpContext context, Func<HttpContext?> currentContext,
        IConfiguration configuration, TimeProvider clock)
    {
        _ = CustomerJournalPrincipal.Require(context, configuration);
        _context = context;
        _currentContext = currentContext;
        _configuration = configuration;
        _clock = clock;
        _principal = _context.User;
        _identity = _principal.Identity;
        _principalBytes = Snapshot(_principal);
        _authorization = [.. _context.Request.Headers.Authorization];
        _workspaces = [.. _context.Request.Headers["X-Workspace-Id"]];
        _storefronts = [.. _context.Request.Headers["X-Storefront-Id"]];
        _path = _context.Request.Path;
        var expiries = _principal.FindAll("exp").ToArray();
        if (expiries.Length != 1 || !long.TryParse(expiries[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            throw Unavailable();
        }

        try
        {
            OriginalTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Unavailable();
        }

        RequireCurrent(default);
    }

    public DateTimeOffset OriginalTokenExpiry { get; }

    public void RequireCurrent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _context.RequestAborted.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_currentContext(), _context) || !ReferenceEquals(_context.User, _principal) ||
            !ReferenceEquals(_principal.Identity, _identity) || _identity?.IsAuthenticated != true ||
            !Snapshot(_principal).AsSpan().SequenceEqual(_principalBytes) || _context.Request.Path != _path ||
            !_context.Request.Headers.Authorization.SequenceEqual(_authorization) ||
            !_context.Request.Headers["X-Workspace-Id"].SequenceEqual(_workspaces) ||
            !_context.Request.Headers["X-Storefront-Id"].SequenceEqual(_storefronts) ||
            OriginalTokenExpiry <= _clock.GetUtcNow())
        {
            throw Unavailable();
        }

        _ = CustomerJournalPrincipal.Require(_context, _configuration);
    }

    private static byte[] Snapshot(ClaimsPrincipal value)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true);
        value.WriteTo(writer);
        writer.Flush();
        return bytes.ToArray();
    }

    private static DeliveryContactFailureException Unavailable() => new("AUTHORITY_UNAVAILABLE");
}
