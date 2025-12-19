using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbitMesh.Host.Authentication;
using OrbitMesh.Host.Services;

namespace OrbitMesh.Host.Controllers;

/// <summary>
/// REST API controller for API token management.
/// Requires admin authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AdminAuthorize]
public class TokensController : ControllerBase
{
    private readonly IApiTokenService _tokenService;

    /// <summary>
    /// Creates a new tokens controller.
    /// </summary>
    public TokensController(IApiTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    /// <summary>
    /// Lists all API tokens.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tokens (without actual token values).</returns>
    /// <response code="200">Tokens retrieved successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiToken>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTokens(CancellationToken cancellationToken = default)
    {
        var tokens = await _tokenService.GetAllTokensAsync(cancellationToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Creates a new API token.
    /// </summary>
    /// <param name="request">The token creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created token (including the token value - only shown once).</returns>
    /// <response code="201">Token created successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiToken), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateToken(
        [FromBody] CreateTokenRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { Error = "Token name is required" });
        }

        var token = await _tokenService.CreateTokenAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListTokens), token);
    }

    /// <summary>
    /// Revokes an API token.
    /// </summary>
    /// <param name="tokenId">The token ID to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    /// <response code="204">Token revoked successfully.</response>
    /// <response code="404">Token not found.</response>
    [HttpDelete("{tokenId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeToken(
        string tokenId,
        CancellationToken cancellationToken = default)
    {
        var revoked = await _tokenService.RevokeTokenAsync(tokenId, cancellationToken);

        if (!revoked)
        {
            return NotFound();
        }

        return NoContent();
    }
}
