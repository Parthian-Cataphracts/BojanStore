using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bojan.Infrastructure.Notifications;

namespace Bojan.Api.Tests;

/// <summary>
/// The Web Push wire format, against the RFCs' own worked examples.
/// </summary>
/// <remarks>
/// This is the only test in the suite that has to exist. Everything else here
/// checks behaviour that would show up in use; a message encryption that is
/// subtly wrong produces a request the push service happily accepts and a
/// browser silently discards, with nothing anywhere saying why. The published
/// vector is the only way to know the construction is right rather than merely
/// self-consistent.
/// </remarks>
public sealed class WebPushCryptoTests
{
    // RFC 8291 Appendix A — every input of the worked example.
    private const string Plaintext = "When I grow up, I want to be a watermelon";
    private const string ReceiverPublicKey = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string SenderPrivateKey = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string SenderPublicKey = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";

    private const string ExpectedBody =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLoc" +
        "InmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLV" +
        "WGNWQexSgSxsj_Qulcy4a-fN";

    private static ECDiffieHellman Sender()
    {
        var publicPoint = WebPushCrypto.FromBase64Url(SenderPublicKey);

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = WebPushCrypto.FromBase64Url(SenderPrivateKey),
            Q = new ECPoint { X = publicPoint[1..33], Y = publicPoint[33..65] },
        });
    }

    [Fact]
    public void The_encrypted_body_matches_the_RFC_8291_example()
    {
        using var sender = Sender();

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            WebPushCrypto.FromBase64Url(ReceiverPublicKey),
            WebPushCrypto.FromBase64Url(AuthSecret),
            WebPushCrypto.FromBase64Url(Salt),
            sender);

        Assert.Equal(ExpectedBody, WebPushCrypto.Base64Url(body));
    }

    /// <summary>
    /// Two messages with the same content must not produce the same bytes. The
    /// salt and the ephemeral key are fresh per message, and a push service
    /// carrying identical ciphertext twice would be able to tell that a customer
    /// received the same notification twice.
    /// </summary>
    [Fact]
    public void Two_sends_of_one_message_do_not_produce_the_same_ciphertext()
    {
        var payload = Encoding.UTF8.GetBytes(Plaintext);
        var key = WebPushCrypto.FromBase64Url(ReceiverPublicKey);
        var auth = WebPushCrypto.FromBase64Url(AuthSecret);

        Assert.NotEqual(
            WebPushCrypto.Base64Url(WebPushCrypto.Encrypt(payload, key, auth)),
            WebPushCrypto.Base64Url(WebPushCrypto.Encrypt(payload, key, auth)));
    }

    /// <summary>
    /// A payload past one record is refused rather than truncated. Truncating
    /// would send a notification whose body ends mid-sentence.
    /// </summary>
    [Fact]
    public void A_payload_too_large_for_one_record_is_refused()
    {
        var oversized = new byte[5_000];

        Assert.Throws<ArgumentOutOfRangeException>(() => WebPushCrypto.Encrypt(
            oversized,
            WebPushCrypto.FromBase64Url(ReceiverPublicKey),
            WebPushCrypto.FromBase64Url(AuthSecret)));
    }

    /// <summary>
    /// Browsers hand over unpadded base64url. `Convert.FromBase64String` refuses
    /// those outright, so decoding them is what stands between the shop and
    /// every real subscription failing to save.
    /// </summary>
    [Theory]
    [InlineData(AuthSecret, 16)]
    [InlineData(ReceiverPublicKey, 65)]
    public void Unpadded_base64url_from_a_browser_decodes(string value, int expectedLength) =>
        Assert.Equal(expectedLength, WebPushCrypto.FromBase64Url(value).Length);

    // --- VAPID, RFC 8292 ----------------------------------------------------

    /// <summary>
    /// The token is what a push service checks to decide the message is from
    /// this shop. Wrong and every notification is rejected; unverifiable and the
    /// header is decoration.
    /// </summary>
    [Fact]
    public void The_vapid_token_verifies_against_the_shops_public_key()
    {
        var (publicKey, privateKey) = WebPushCrypto.GenerateKeyPair();
        var now = DateTimeOffset.UtcNow;

        var header = WebPushCrypto.VapidHeader(
            new Uri("https://fcm.googleapis.com/fcm/send/abc123"),
            "mailto:shop@bojan.test",
            WebPushCrypto.FromBase64Url(publicKey),
            WebPushCrypto.FromBase64Url(privateKey),
            now);

        var token = header["vapid t=".Length..header.IndexOf(", k=", StringComparison.Ordinal)];
        var parts = token.Split('.');

        Assert.Equal(3, parts.Length);

        var point = WebPushCrypto.FromBase64Url(publicKey);

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });

        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            WebPushCrypto.FromBase64Url(parts[2]),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        // The audience is the push service's origin and nothing else. A token
        // carrying the full endpoint path names a URL rather than a service, and
        // is rejected.
        using var claims = JsonDocument.Parse(WebPushCrypto.FromBase64Url(parts[1]));

        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal("mailto:shop@bojan.test", claims.RootElement.GetProperty("sub").GetString());
        Assert.True(claims.RootElement.GetProperty("exp").GetInt64() > now.ToUnixTimeSeconds());
    }

    [Fact]
    public void A_generated_key_pair_is_the_shape_browsers_expect()
    {
        var (publicKey, privateKey) = WebPushCrypto.GenerateKeyPair();

        var point = WebPushCrypto.FromBase64Url(publicKey);

        // 65 bytes, uncompressed — what `applicationServerKey` takes.
        Assert.Equal(65, point.Length);
        Assert.Equal(0x04, point[0]);
        Assert.Equal(32, WebPushCrypto.FromBase64Url(privateKey).Length);

        // base64url, so it survives being handed to a browser in JSON and put
        // straight into `PushManager.subscribe`.
        Assert.DoesNotContain('+', publicKey);
        Assert.DoesNotContain('/', publicKey);
        Assert.DoesNotContain('=', publicKey);
    }
}
