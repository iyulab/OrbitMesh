using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrbitMesh.Host.Authentication;

/// <summary>
/// Extension methods for configuring admin authentication.
/// </summary>
public static class AdminAuthenticationExtensions
{
    /// <summary>
    /// The authentication scheme name for admin authentication.
    /// </summary>
    public const string SchemeName = "AdminAuth";

    /// <summary>
    /// The authorization policy name for admin-only endpoints.
    /// </summary>
    public const string PolicyName = "AdminPolicy";

    /// <summary>
    /// The configuration key for admin password in appsettings.json.
    /// </summary>
    public const string ConfigurationKey = "OrbitMesh:AdminPassword";

    /// <summary>
    /// Adds admin authentication services.
    /// Password is read from environment variable or appsettings.json.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (for reading from appsettings.json).</param>
    /// <returns>The authentication builder for further configuration.</returns>
    public static AuthenticationBuilder AddAdminAuthentication(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        return services.AddAdminAuthentication(configuration, _ => { });
    }

    /// <summary>
    /// Adds admin authentication services with custom configuration.
    /// Password priority: 1) Environment variable, 2) appsettings.json, 3) custom configure action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (for reading from appsettings.json).</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The authentication builder for further configuration.</returns>
    public static AuthenticationBuilder AddAdminAuthentication(
        this IServiceCollection services,
        IConfiguration? configuration,
        Action<AdminAuthenticationOptions> configure)
    {
        var builder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = SchemeName;
            options.DefaultChallengeScheme = SchemeName;
        });

        builder.AddScheme<AdminAuthenticationOptions, AdminAuthenticationHandler>(
            SchemeName,
            options =>
            {
                // Priority 1: Environment variable
                var envPassword = Environment.GetEnvironmentVariable(
                    AdminAuthenticationOptions.EnvironmentVariableName);

                if (!string.IsNullOrEmpty(envPassword))
                {
                    options.AdminPassword = envPassword;
                }
                // Priority 2: appsettings.json (OrbitMesh:AdminPassword)
                else if (configuration != null)
                {
                    var configPassword = configuration[ConfigurationKey];
                    if (!string.IsNullOrEmpty(configPassword))
                    {
                        options.AdminPassword = configPassword;
                    }
                }

                // Priority 3: Custom configuration action
                configure(options);
            });

        // Add authorization policy for admin endpoints
        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyName, policy =>
            {
                policy.AuthenticationSchemes.Add(SchemeName);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });

        return builder;
    }

    /// <summary>
    /// Adds admin authentication with a specific password (for testing).
    /// In production, use environment variable instead.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="password">The admin password.</param>
    /// <returns>The authentication builder for further configuration.</returns>
    public static AuthenticationBuilder AddAdminAuthentication(
        this IServiceCollection services,
        string password)
    {
        return services.AddAdminAuthentication(null, options =>
        {
            options.AdminPassword = password;
        });
    }
}

/// <summary>
/// Attribute to require admin authentication on controllers or actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AdminAuthorizeAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Creates a new admin authorize attribute.
    /// </summary>
    public AdminAuthorizeAttribute()
    {
        Policy = AdminAuthenticationExtensions.PolicyName;
        AuthenticationSchemes = AdminAuthenticationExtensions.SchemeName;
    }
}
