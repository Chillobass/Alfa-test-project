using alfa_test_proj.Core.Email;

namespace alfa_test_proj.Core.Tests;

public class EmailAddressValidatorTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("  first.last+tag@mail.example.ru  ")]
    [InlineData("ivan@почта.рф")]
    public void ValidAddresses(string address)
    {
        Assert.True(EmailAddressValidator.TryNormalize(address, out var normalized, out _));
        Assert.Equal(address.Trim(), normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("user")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("user@example")]
    [InlineData("user@@example.com")]
    [InlineData("us er@example.com")]
    [InlineData("user..name@example.com")]
    [InlineData("user@example..com")]
    [InlineData("user@-example.com")]
    [InlineData("a@b.c, d@e.f")]
    public void InvalidAddresses(string address)
    {
        Assert.False(EmailAddressValidator.TryNormalize(address, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
