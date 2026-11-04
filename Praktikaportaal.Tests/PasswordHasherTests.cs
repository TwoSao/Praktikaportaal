using Praktikaportaal.Infrastructure.Services;
using Xunit;

namespace Praktikaportaal.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnValidFormattedString()
    {
        string password = "StrongPassword123!";
        string hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        string[] parts = hash.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal("100000", parts[0]);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        string password = "CorrectHorseBatteryStaple!";
        string hash = _hasher.HashPassword(password);

        bool isValid = _hasher.VerifyPassword(password, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        string password = "CorrectHorseBatteryStaple!";
        string wrongPassword = "WrongPassword!";
        string hash = _hasher.HashPassword(password);

        bool isValid = _hasher.VerifyPassword(wrongPassword, hash);

        Assert.False(isValid);
    }

    [Fact]
    public void HashPassword_GeneratesUniqueSaltsForSamePassword()
    {
        string password = "SamePassword123";

        string hash1 = _hasher.HashPassword(password);
        string hash2 = _hasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.VerifyPassword(password, hash1));
        Assert.True(_hasher.VerifyPassword(password, hash2));
    }
}
