using BitwardenSharp.Domain.Uris;
using Shouldly;
using Xunit;

namespace BitwardenSharp.Domain.Tests;

public class UriTargetSpecs
{
    [Theory]
    [InlineData("https://www.example.com/login", "example.com")]
    [InlineData("https://auth.digikey.com/as/authorization.oauth2", "digikey.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("HTTPS://WWW.EXAMPLE.COM/", "example.com")]
    [InlineData("https://user:pass@example.com/x", "example.com")]
    [InlineData("https://example.com:8443/", "example.com")]
    [InlineData("https://example.com./", "example.com")]
    public void Reduces_a_web_uri_to_its_registrable_domain(string uri, string expected)
    {
        var target = UriTarget.Parse(uri);

        target.ShouldNotBeNull();
        target.Kind.ShouldBe(UriTargetKind.Domain);
        target.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://www.countdown.co.nz/shop", "countdown.co.nz")]
    [InlineData("https://sso.countdown.co.nz/", "countdown.co.nz")]
    [InlineData("https://www.amazon.com.au/ap/signin", "amazon.com.au")]
    [InlineData("https://www.bbc.co.uk/news", "bbc.co.uk")]
    [InlineData("https://tracing.covid19.govt.nz/x", "covid19.govt.nz")]
    public void Respects_multi_label_public_suffixes(string uri, string expected)
    {
        UriTarget.Parse(uri)!.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://192.168.1.1:8443/", "192.168.1.1")]
    [InlineData("10.0.0.11", "10.0.0.11")]
    public void Treats_a_literal_ip_as_its_own_kind(string uri, string expected)
    {
        var target = UriTarget.Parse(uri);

        target!.Kind.ShouldBe(UriTargetKind.IpAddress);
        target.Value.ShouldBe(expected);
        // An IP has no brand, so it can never be folded together with a domain by brand rules.
        target.Brand.ShouldBeNull();
    }

    [Theory]
    [InlineData("localhost:3000", "localhost")]
    [InlineData("http://synology/", "synology")]
    public void Treats_a_dotless_host_as_its_own_kind(string uri, string expected)
    {
        var target = UriTarget.Parse(uri);

        target!.Kind.ShouldBe(UriTargetKind.Host);
        target.Value.ShouldBe(expected);
    }

    [Fact]
    public void Keeps_native_app_ids_out_of_the_domain_namespace()
    {
        var target = UriTarget.Parse("androidapp://com.google.android.gm");

        target!.Kind.ShouldBe(UriTargetKind.App);
        target.Value.ShouldBe("com.google.android.gm");
        target.Brand.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    public void Yields_nothing_for_an_unusable_uri(string? uri) => UriTarget.Parse(uri).ShouldBeNull();

    [Fact]
    public void Brand_is_the_leading_label_of_a_registrable_domain()
    {
        UriTarget.Parse("https://www.digikey.co.nz/")!.Brand.ShouldBe("digikey");
        UriTarget.Parse("https://auth.digikey.com/")!.Brand.ShouldBe("digikey");
    }

    [Theory]
    [InlineData("https://mail.google.com/", "google")]
    [InlineData("https://www.youtube.com/", "google")]
    [InlineData("https://outlook.live.com/", "microsoft")]
    [InlineData("https://login.microsoftonline.com/", "microsoft")]
    public void Maps_a_known_brand_to_its_service_family(string uri, string family)
    {
        ServiceFamily.ForTarget(UriTarget.Parse(uri)!).ShouldBe(family);
    }

    [Fact]
    public void An_unknown_brand_has_no_family()
    {
        ServiceFamily.ForTarget(UriTarget.Parse("https://www.geekzone.co.nz/")!).ShouldBeNull();
    }
}
