using BitwardenManager.Core.Models;
using BitwardenManager.CliWrapper;

namespace BitwardenManager.Tests;

public class BitwardenCliServiceTests
{
    [Fact]
    public void BitwardenCliService_ShouldInitialize_WithDefaultPath()
    {
        // Arrange & Act
        var service = new BitwardenCliService();
        
        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void BitwardenCliService_ShouldInitialize_WithCustomPath()
    {
        // Arrange
        var customPath = "/custom/path/to/bw";
        
        // Act
        var service = new BitwardenCliService(customPath);
        
        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task IsAuthenticatedAsync_ShouldReturnFalse_WhenCliNotAvailable()
    {
        // Arrange
        var service = new BitwardenCliService("nonexistent-cli");
        
        // Act
        var result = await service.IsAuthenticatedAsync();
        
        // Assert
        Assert.False(result);
    }
}

public class VaultItemTests
{
    [Fact]
    public void VaultItem_ShouldInitialize_WithDefaults()
    {
        // Arrange & Act
        var item = new VaultItem();
        
        // Assert
        Assert.Equal(string.Empty, item.Name);
        Assert.False(item.Favorite);
        Assert.False(item.Reprompt);
        Assert.Empty(item.Fields);
        Assert.Empty(item.Attachments);
    }

    [Fact]
    public void VaultItem_ShouldSet_Properties()
    {
        // Arrange & Act
        var item = new VaultItem
        {
            Id = "test-id",
            Name = "Test Item",
            Type = ItemType.Login,
            Favorite = true,
            Notes = "Test notes"
        };
        
        // Assert
        Assert.Equal("test-id", item.Id);
        Assert.Equal("Test Item", item.Name);
        Assert.Equal(ItemType.Login, item.Type);
        Assert.True(item.Favorite);
        Assert.Equal("Test notes", item.Notes);
    }
}

public class ConfigurationTests
{
    [Fact]
    public void BitwardenConfiguration_ShouldHave_DefaultTimeoutMinutes()
    {
        // Arrange & Act
        var config = new BitwardenConfiguration();
        
        // Assert
        Assert.Equal(15, config.TimeoutMinutes);
    }

    [Fact]
    public void AuthenticationResult_ShouldInitialize_WithDefaults()
    {
        // Arrange & Act
        var result = new AuthenticationResult();
        
        // Assert
        Assert.False(result.Success);
        Assert.False(result.RequiresTwoFactor);
    }
}
