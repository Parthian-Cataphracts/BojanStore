using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// The Web Push wire format: message encryption (RFC 8291) and the identity
/// header that proves who sent it (RFC 8292).
/// </summary>
/// <remarks>
/// <para>
/// Written against the RFCs rather than taken from a package. Everything it
/// needs — P-256, ECDH, HKDF, AES-GCM, ECDSA — is in the BCL, so a dependency
/// here would buy about a hundred lines and cost another third party inside the
/// path that handles customers' notification content. <see
/// cref="WebPushCryptoTests"/> pins it against the worked example in RFC 8291
/// Appendix A, which is the only way to know a construction like this is right.
/// </para>
/// <para>
/// The shape of it: the shop generates a throwaway P-256 key pair per message,
/// does ECDH against the browser's public key, mixes in the browser's auth
/// secret, and derives a content key and nonce. Only that browser holds the
/// other half, so the push service carrying the message cannot read it — which
/// is the point, since the message is a customer's own order news travelling
/// through Google's or Mozilla's infrastructure.
/// </para>
/// </remarks>
internal static class WebPushCrypto
{
    /// <summary>Uncompressed P-256 point: <c>0x04</c> and two 32-byte coordinates.</summary>
    private const int PublicKeyLength = 65;

    /// <summary>The record size in the aes128gcm header. One record, so it only has to be big enough.</summary>
    private const int RecordSize = 4096;

    /// <summary>
    /// The largest payload that fits one record: 4096 less the 16-byte GCM tag
    /// and the one-byte delimiter.
    /// </summary>
    internal const int MaxPayloadBytes = RecordSize - 17;

    /// <summary>
    /// How long a VAPID token stands.
    /// </summary>
    /// <remarks>
    /// Twelve hours rather than the twenty-four the spec permits: the token is
    /// the shop's proof of identity to the push service, and a shorter life is a
    /// shorter window for one that leaks out of a log.
    /// </remarks>
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// Seals a message for one browser.
    /// </summary>
    /// <returns>The request body, header and ciphertext together.</returns>
    internal static byte[] Encrypt(byte[] payload, byte[] userPublicKey, byte[] authSecret)
    {
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        return Encrypt(payload, userPublicKey, authSecret, RandomNumberGenerator.GetBytes(16), ephemeral);
    }

    /// <summary>
    /// The same thing with the two random inputs supplied.
    /// </summary>
    /// <remarks>
    /// A seam for the tests and nothing else. RFC 8291's worked example fixes
    /// the salt and the sender's key pair, and without being able to set both
    /// there is no way to check this implementation against it — only against
    /// itself, which would pass just as happily if the whole construction were
    /// wrong.
    /// </remarks>
    internal static byte[] Encrypt(
        byte[] payload,
        byte[] userPublicKey,
        byte[] authSecret,
        byte[] salt,
        ECDiffieHellman ephemeral)
    {
        if (payload.Length > MaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload does not fit one aes128gcm record.");
        }

        if (userPublicKey.Length != PublicKeyLength)
        {
            throw new ArgumentException("A subscription key must be an uncompressed P-256 point.", nameof(userPublicKey));
        }

        var serverPublicKey = ExportPoint(ephemeral.ExportParameters(false));

        using var browser = ECDiffieHellman.Create(ImportPoint(userPublicKey));

        // The raw agreement, not a hashed one: RFC 8291 feeds the X coordinate
        // itself into HKDF, and DeriveKeyFromHash would hand back something else
        // entirely.
        var sharedSecret = ephemeral.DeriveRawSecretAgreement(browser.PublicKey);

        // The browser's auth secret is the salt of the first extraction, so a
        // message can only be opened by a browser that also holds it — an
        // attacker who somehow obtained the ECDH agreement alone still cannot.
        var keyInfo = Concat(
            Encoding.ASCII.GetBytes("WebPush: info\0"),
            userPublicKey,
            serverPublicKey);

        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);

        var contentKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));

        var nonce = HKDF.DeriveKey(
            HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // 0x02 marks the last record. One record is always the last one, and a
        // 0x01 here is the difference between a notification and a browser that
        // silently drops it.
        var plaintext = new byte[payload.Length + 1];
        payload.CopyTo(plaintext, 0);
        plaintext[^1] = 0x02;

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(contentKey, tag.Length))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // RFC 8188 header: salt, record size, then the ephemeral key the browser
        // needs to derive the same secret. The key travels in the clear because
        // it is a public key that exists only for this one message.
        var header = new byte[16 + 4 + 1 + PublicKeyLength];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16, 4), RecordSize);
        header[20] = PublicKeyLength;
        serverPublicKey.CopyTo(header, 21);

        return Concat(header, ciphertext, tag);
    }

    /// <summary>
    /// The <c>Authorization</c> header proving the message came from this shop.
    /// </summary>
    /// <remarks>
    /// Push services accept messages for a browser from anyone who has the
    /// endpoint URL. VAPID is what lets them tell one sender from another — the
    /// browser recorded this public key when it subscribed, so a message signed
    /// by any other key is not from the shop the customer agreed to hear from.
    /// </remarks>
    internal static string VapidHeader(Uri endpoint, string subject, byte[] publicKey, byte[] privateKey, DateTimeOffset nowUtc)
    {
        var audience = $"{endpoint.Scheme}://{endpoint.Authority}";

        var header = Base64Url(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));

        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = audience,
            ["exp"] = nowUtc.Add(TokenLifetime).ToUnixTimeSeconds(),
            ["sub"] = subject,
        }));

        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = privateKey,
            Q = ImportPoint(publicKey).Q,
        });

        // JOSE wants r and s concatenated at fixed width. The default here is
        // DER, which push services reject as a malformed token.
        var signature = key.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"vapid t={header}.{claims}.{Base64Url(signature)}, k={Base64Url(publicKey)}";
    }

    /// <summary>Mints the shop's own identity key pair — the panel's "generate" button.</summary>
    internal static (string PublicKey, string PrivateKey) GenerateKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);

        return (Base64Url(ExportPoint(parameters)), Base64Url(parameters.D!));
    }

    /// <summary>
    /// Decodes base64url, tolerating the padding browsers omit.
    /// </summary>
    /// <remarks>
    /// The subscription keys arrive from `PushSubscription.getKey()`, which is
    /// unpadded. `Convert.FromBase64String` refuses those outright, and the
    /// symptom would be every subscription from a real browser failing to save.
    /// </remarks>
    internal static byte[] FromBase64Url(string value)
    {
        var normalised = value.Trim().Replace('-', '+').Replace('_', '/');
        var padding = normalised.Length % 4;

        if (padding is 2 or 3)
        {
            normalised = normalised.PadRight(normalised.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(normalised);
    }

    internal static string Base64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static ECParameters ImportPoint(byte[] uncompressed) => new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint
        {
            X = uncompressed[1..33],
            Y = uncompressed[33..65],
        },
    };

    private static byte[] ExportPoint(ECParameters parameters)
    {
        var point = new byte[PublicKeyLength];
        point[0] = 0x04;
        parameters.Q.X!.CopyTo(point, 1);
        parameters.Q.Y!.CopyTo(point, 33);
        return point;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(part => part.Length)];
        var offset = 0;

        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
