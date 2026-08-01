using Bojan.Application.Auth;
using Bojan.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bojan.Api.Tests;

/// <summary>
/// The fixed development sign-in code, and the two things that keep it out of
/// production.
/// </summary>
/// <remarks>
/// These are unit tests rather than endpoint tests on purpose: what needs
/// proving is not that the code works — the OTP round trip is already covered —
/// but that the decorator is inert for every number except the configured one,
/// and inert entirely when nothing is configured. The third guard, that
/// <c>Program.cs</c> only registers it in Development, is a one-line
/// <c>if</c> that a test cannot meaningfully assert on without hosting a second
/// environment; it is written where it is easy to read instead.
/// </remarks>
public sealed class DevelopmentSignInTests
{
    private static StaticOtpCodeGenerator Generator(string phone, string code) => new(
        new RandomOtpCodeGenerator(),
        Options.Create(new DevOtpOptions { Phone = phone, Code = code }),
        NullLogger<StaticOtpCodeGenerator>.Instance);

    [Fact]
    public void The_configured_number_always_gets_the_configured_code()
    {
        var generator = Generator("09123456789", "11111");

        Assert.Equal("11111", generator.GenerateFor("09123456789"));
        Assert.Equal("11111", generator.GenerateFor("09123456789"));
    }

    [Fact]
    public void Every_other_number_still_gets_a_random_five_digit_code()
    {
        var generator = Generator("09123456789", "11111");

        var first = generator.GenerateFor("09121110001");
        var second = generator.GenerateFor("09121110001");

        Assert.Equal(5, first.Length);
        Assert.All(first, character => Assert.True(char.IsAsciiDigit(character)));

        // Two draws matching would be a 1-in-100,000 coincidence; a fixed code
        // leaking to other numbers would fail this every time.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void With_nothing_configured_no_number_is_special()
    {
        var generator = Generator(string.Empty, string.Empty);

        var code = generator.GenerateFor("09123456789");

        Assert.Equal(5, code.Length);
        Assert.NotEqual("11111", generator.GenerateFor("09123456789"));
        Assert.NotEqual(code, generator.GenerateFor("09123456789"));
    }

    [Fact]
    public void A_phone_configured_without_a_code_does_not_count_as_configured()
    {
        var generator = Generator("09123456789", string.Empty);

        // Half-configured must mean off, not "the empty string is your code".
        Assert.NotEqual(string.Empty, generator.GenerateFor("09123456789"));
    }

    /// <summary>
    /// The default registration — the one every environment gets — hands out
    /// random codes and knows nothing about any configured number.
    /// </summary>
    [Fact]
    public void The_production_generator_has_no_notion_of_a_fixed_code()
    {
        var generator = new RandomOtpCodeGenerator();

        var codes = Enumerable.Range(0, 20).Select(_ => generator.GenerateFor("09123456789")).ToList();

        Assert.All(codes, code => Assert.Equal(5, code.Length));
        Assert.True(codes.Distinct().Count() > 1);
    }
}
