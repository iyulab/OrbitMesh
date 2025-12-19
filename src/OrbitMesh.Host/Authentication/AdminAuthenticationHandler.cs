using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrbitMesh.Host.Authentication;

/// <summary>
/// Authentication handler for admin password-based authentication.
/// Supports both Basic authentication and X-Admin-Password header.
/// </summary>
public sealed class AdminAuthenticationHandler : AuthenticationHandler<AdminAuthenticationOptions>
{
    private const string AdminPasswordHeader = "X-Admin-Password";
    private const string AuthorizationHeader = "Authorization";
    private const string BasicScheme = "Basic";

    /// <summary>
    /// Creates a new admin authentication handler.
    /// </summary>
    public AdminAuthenticationHandler(
        IOptionsMonitor<AdminAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Get the configured admin password
        var expectedPassword = Options.AdminPassword;

        // If no password configured, deny all admin access
        if (string.IsNullOrEmpty(expectedPassword))
        {
            Logger.LogWarning(
                "Admin authentication failed: ORBITMESH_ADMIN_PASSWORD environment variable not set");
            return Task.FromResult(AuthenticateResult.Fail(
                "Admin password not configured. Set ORBITMESH_ADMIN_PASSWORD environment variable."));
        }

        // Try X-Admin-Password header first
        if (Request.Headers.TryGetValue(AdminPasswordHeader, out var passwordHeader))
        {
            var providedPassword = passwordHeader.ToString();
            if (ValidatePassword(providedPassword, expectedPassword))
            {
                return Task.FromResult(AuthenticateResult.Success(CreateTicket("admin")));
            }

            Logger.LogWarning("Admin authentication failed: Invalid password in X-Admin-Password header");
            return Task.FromResult(AuthenticateResult.Fail("Invalid admin password"));
        }

        // Try Basic authentication
        if (Request.Headers.TryGetValue(AuthorizationHeader, out var authHeader))
        {
            var authValue = authHeader.ToString();
            if (authValue.StartsWith(BasicScheme + " ", StringComparison.OrdinalIgnoreCase))
            {
                var encodedCredentials = authValue[(BasicScheme.Length + 1)..];
                try
                {
                    var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                    var parts = credentials.Split(':', 2);

                    if (parts.Length == 2)
                    {
                        var username = parts[0];
                        var password = parts[1];

                        // Accept any username with correct password, or admin:password
                        if (ValidatePassword(password, expectedPassword))
                        {
                            return Task.FromResult(AuthenticateResult.Success(
                                CreateTicket(string.IsNullOrEmpty(username) ? "admin" : username)));
                        }
                    }
                }
                catch (FormatException)
                {
                    // Invalid base64
                }

                Logger.LogWarning("Admin authentication failed: Invalid credentials in Authorization header");
                return Task.FromResult(AuthenticateResult.Fail("Invalid admin credentials"));
            }
        }

        // No credentials provided
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;

        // Only send WWW-Authenticate header for non-AJAX requests to avoid browser popup
        // AJAX requests will handle 401 in JavaScript
        var isAjaxRequest = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || Request.Headers.XRequestedWith == "XMLHttpRequest"
            || Request.Headers.ContainsKey(AdminPasswordHeader);

        if (!isAjaxRequest)
        {
            Response.Headers.WWWAuthenticate = $"{BasicScheme} realm=\"OrbitMesh Admin\"";
        }

        return Task.CompletedTask;
    }

    private static bool ValidatePassword(string provided, string expected)
    {
        // Use constant-time comparison to prevent timing attacks
        if (provided.Length != expected.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < provided.Length; i++)
        {
            result |= provided[i] ^ expected[i];
        }

        return result == 0;
    }

    private AuthenticationTicket CreateTicket(string username)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("admin", "true")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationTicket(principal, Scheme.Name);
    }
}

/// <summary>
/// Options for admin authentication.
/// </summary>
public sealed class AdminAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The environment variable name for admin password.
    /// </summary>
    public const string EnvironmentVariableName = "ORBITMESH_ADMIN_PASSWORD";

    /// <summary>
    /// Gets or sets the admin password.
    /// </summary>
    public string? AdminPassword { get; set; }
}
