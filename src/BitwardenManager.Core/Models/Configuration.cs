namespace BitwardenManager.Core.Models;

public class BitwardenConfiguration
{
    public string? ServerUrl { get; set; }
    public string? ApiUrl { get; set; }
    public string? IdentityUrl { get; set; }
    public string? IconsUrl { get; set; }
    public string? NotificationsUrl { get; set; }
    public string? EventsUrl { get; set; }
    public string? WebVaultUrl { get; set; }
    public int TimeoutMinutes { get; set; } = 15;
    public bool? DisableFavicon { get; set; }
    public string? EnvironmentUrls { get; set; }
    public bool SelfHosted { get; set; }
}

public class AuthenticationResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorProviders { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BitwardenStatus
{
    public bool IsAuthenticated { get; set; }
    public bool IsLocked { get; set; }
    public string? ServerUrl { get; set; }
    public DateTime? LastSync { get; set; }
    public string? UserEmail { get; set; }
    public string? UserId { get; set; }
}
