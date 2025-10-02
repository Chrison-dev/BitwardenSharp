using Microsoft.AspNetCore.Mvc;
using BitwardenManager.Core.Interfaces;
using BitwardenManager.Core.Models;

namespace BitwardenManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BitwardenController : ControllerBase
{
    private readonly IBitwardenService _bitwardenService;
    private readonly ILogger<BitwardenController> _logger;

    public BitwardenController(IBitwardenService bitwardenService, ILogger<BitwardenController> logger)
    {
        _bitwardenService = bitwardenService;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<object>> GetStatus()
    {
        try
        {
            var isAuthenticated = await _bitwardenService.IsAuthenticatedAsync();
            return Ok(new { IsAuthenticated = isAuthenticated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return StatusCode(500, new { Error = "Failed to check authentication status" });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var success = await _bitwardenService.AuthenticateAsync(
                request.Email, 
                request.Password, 
                request.TwoFactorCode);
            
            if (success)
            {
                return Ok(new { Success = true, Message = "Login successful" });
            }
            
            return BadRequest(new { Success = false, Message = "Login failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { Error = "Login failed due to an error" });
        }
    }

    [HttpPost("unlock")]
    public async Task<ActionResult<object>> Unlock([FromBody] UnlockRequest request)
    {
        try
        {
            var success = await _bitwardenService.UnlockAsync(request.MasterPassword);
            
            if (success)
            {
                return Ok(new { Success = true, Message = "Vault unlocked successfully" });
            }
            
            return BadRequest(new { Success = false, Message = "Failed to unlock vault" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unlock");
            return StatusCode(500, new { Error = "Unlock failed due to an error" });
        }
    }

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<VaultItem>>> GetVaultItems()
    {
        try
        {
            var items = await _bitwardenService.GetVaultItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vault items");
            return StatusCode(500, new { Error = "Failed to retrieve vault items" });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<VaultItem>>> SearchVaultItems([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { Error = "Search query is required" });
            }
            
            var items = await _bitwardenService.SearchVaultItemsAsync(query);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vault items");
            return StatusCode(500, new { Error = "Failed to search vault items" });
        }
    }

    public record LoginRequest(string Email, string Password, string? TwoFactorCode = null);
    public record UnlockRequest(string MasterPassword);
}