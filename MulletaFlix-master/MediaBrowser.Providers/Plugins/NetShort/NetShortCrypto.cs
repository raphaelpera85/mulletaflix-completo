using System;
using System.Security.Cryptography;
using System.Text;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// Reimplements the payload encryption that the NetShort web client applies to every call of
    /// its JSON API. Without it the API answers HTTP 500 to any plain-text body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything below was read out of the site's own JavaScript bundle
    /// (<c>/_next/static/chunks/8000-e7b841909b9e1a44.js</c>, webpack module 97539 for the cipher and
    /// module 6188 for the transport), then verified against a captured session and finally against
    /// the live API. The scheme has three independent parts:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// <b>Request body.</b> AES-256 in ECB mode with PKCS#7 padding, over the UTF-8 JSON, encoded as
    /// base64. The key is the 32-byte ASCII string <see cref="PayloadKey"/>. It is a compile-time
    /// constant in the bundle: the "random key" branch in the client is dead code, because it builds
    /// its result with <c>String.prototype.charAt</c> (which returns a new string that is discarded)
    /// and then returns a second hardcoded literal. Two different requests carrying the same body
    /// therefore produce byte-identical ciphertext, which is exactly what the captured session shows
    /// (<c>cZSeHVTvqy+IeOCRjILPxA==</c> for the empty <c>{}</c> body of three different endpoints).
    /// There is no IV, because ECB has none.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Request header <c>encrypt-key</c>.</b> RSA-2048 with PKCS#1 v1.5 padding over the base64
    /// of the same 32 key bytes, using <see cref="RequestPublicKeyPem"/>, base64 encoded. This is the
    /// server's own public key, so the value cannot be decrypted by anyone but the server. It does not
    /// have to be: the AES key is a constant, so a client only has to be able to *produce* the header.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Response body.</b> When the response carries an <c>encrypt-key</c> header the body is
    /// AES-256-ECB/PKCS#7 under a per-response random key. That key is itself the base64 of 32 ASCII
    /// bytes, RSA-PKCS#1-v1.5 encrypted with the *client's* public key so that it can be unwrapped
    /// with <see cref="ResponsePrivateKeyPem"/>. This is the one private key the bundle ships, which
    /// is why the provider can read responses at all.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Proof, against captured traffic: the login response's <c>encrypt-key</c> unwraps to
    /// <c>bDE5cm42ampxZXp5YWIxMmtqdDg5ZmJ0cWI0aGNpam0=</c>, whose 32 decoded bytes are the ASCII
    /// key, and decrypting that response's body with it yields the visitor JWT. The same two steps
    /// applied live return the real search result for "perdões".
    /// </para>
    /// <para>
    /// The keys are public by construction: they are served to every browser that loads the site.
    /// They are reproduced here verbatim, and only for the requests this provider has to make.
    /// </para>
    /// </remarks>
    public static class NetShortCrypto
    {
        /// <summary>
        /// The fixed AES-256 key the NetShort client uses for every request body.
        /// </summary>
        public const string PayloadKey = "5k3KYTOO9jnO0CeyGhdHc3pIjGnVgrMN";

        /// <summary>
        /// The server public key used to build the request <c>encrypt-key</c> header.
        /// </summary>
        /// <remarks>
        /// Its modulus does not match <see cref="ResponsePrivateKeyPem"/>: they are two different
        /// pairs. The site encrypts requests to the server and responses to the browser, and the
        /// browser holds only the private half of the second pair.
        /// </remarks>
        private const string RequestPublicKeyPem = """
            -----BEGIN PUBLIC KEY-----
            MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA2poXMstZ8NCWE7915MXz
            DWC5/t+oB2waGfskPqSZwLqxd4ZBR0H1cb1tAZRZcV7P+LmOd6SYNxhnELaWuKTD
            +D3xkz8Tt1L5j/ynGqVt1MDbiQIEzXQKUkNDSH6T0A+Xzo/67/8QOQXlVJfW06res
            baeNvibfx6Qc78j96bCIPlxPrtieilVTBHUFOXjirxK/ki/mO8P2smRbpt73fsQW
            dGmTGMfYGvfPApGyxbxLkL/qrBjU25XpM8a0MBqzFWUAchHmqSBJ6Mbfam1SSgf3
            b2U28s67nOW+JiOrhd6iVLcsLFxXA54HX+Zbej3AbOB6jKaEmp/bz1amneE1NYXw
            wIDAQAB
            -----END PUBLIC KEY-----
            """;

        /// <summary>
        /// The client private key used to unwrap the response <c>encrypt-key</c> header.
        /// </summary>
        private const string ResponsePrivateKeyPem = """
            -----BEGIN PRIVATE KEY-----
            MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQCK0Tl1pd7bjTRU
            93bWoHW1hLCDj2+9bg1MgY8j5C7xXaw6bJfToXhWbH1fXNbnFFVqxyYNErcuOUwJ
            ZxyDgcxUXM4yWnRseb2GF97GOicAQ2keDzVYmwky4lrSRwvcXutJRLPUCRQNfc6u
            pfk2G5TKh6/CcP4TV1eXTF7+vdEw2SHxAOITKbSfcaZXr/hVs6a1aRHsBF+7RG99
            ebwZIP6/AgIyqX9RbDVN6ixi1v2G3/bwAULHLSqGdSaqij/ca17fbFGITaeCeEaZ
            6d/P4ZuOK+PEPdbPQt6SbY4lZaYwRvdrpH73kigPITgDzIDONFybJ1m7wRKlq1wx
            WHwbimptAgMBAAECggEAPz3cYJXFtt5YphDrahJGLgEabYVOUc2ub1li/eX54Opd
            CWzpqneYnD7myyg/m5zu4SuDUVdibsOZuXrpSZw7m3+ATP5apgS8bDe5vTNHC16q
            qBAjrI9NHIp09/F4HNh9dq6/Am10XkUfgP+KTrU4DyDL2NijV+pltD8N1B5kDE1i
            gokVcsavhnu2INoMRXYE78Wq6urNECuFWw9hldv81M9m2w56t1CQOUukpo4mfmLj
            ZRe2s+kwtcBVefGHP8Cj0OeH2dGltjl2YSQMRBFUCVoixYpOrcjIHoqzWri8IfUZ
            2tW+nUvHl5IZ9RVxefnFaLGnxiXd2sk6Sn4aD/l9YQKBgQDVv3HaOZxHRqlNSPrN
            GqplGhE066HnDsq6MlPukiovxE43CRBmpTnk9zDCqrDh9t2HbJuao7nSq5WlBERW
            gwqXU/qDpH43W7Y/lJfHkDv6A2m0viJa0a9x8+CJpNnCDu1ATo4/IQKwoXYice6J
            KnUyXgkGKn+HipiN6tO0EtWHlQKBgQCmQfklKFtXtm/FZ6NIMs+d+EyvaE5xNLKG
            YQxmiCR10WGYd8ZV+K0Q6qXHS+a32TirWB9F3TqPOklTytMrfPZB3BCXj4weEldb
            8W716G8FYf7LLhaT+MdpF7KDcruObwoQAvKV3N4eX6tUEMmdrx9hpCmmIU5EeXUk
            hGdmwk7BeQKBgAIXMkThJV8pGMTRvuo8pYgBnkN3PoklAuSZU2rU8Sawc9dj9k4a
            tZtAs7BjvQEoyffmHwt/KHUgCoGnrgdulq7uOlgJRtbBxeGPUYC5L2z9lY4YAfwD
            awThTsPp4dtdDAMCAbAqYX1axu4FUUD0MltAwjPWPJMVzvIsZs+vE3mVAoGAJPja
            3OaCmZjadj2709xoyypic0dw2j/ry3JdfZec9A5h87P/CTNJ2U81GoLIhe3qakAo
            hDLUSPGfSOD74NnjMXYswmeLs0xE3Q9tq4XK2pmWPby8DJ/wSHCapByplN0gkbr
            2E1mQk5SW1xT8oPJGukH1eRpC+3s/D6XaEMH5HZECgYEAigoX5l39LDsCgeaUcI4S
            9grkaas/WsKv37eqo3oD9Qk6VFiMM5L5Zig6aXJxuAPLVjb38caJRPmPmOXLT2kE
            P1E1h6OJOhEhETwVIUtcBzsK25ju9LqL89bC+W0uS7BPvk6Tcws/tXHCkQCTgb9j
            VXceZ2ox+6axvlW/5WgHt5Q=
            -----END PRIVATE KEY-----
            """;

        private static readonly byte[] AesKey = Encoding.UTF8.GetBytes(PayloadKey);

        /// <summary>
        /// Encrypts a JSON request body exactly as the NetShort web client does.
        /// </summary>
        /// <param name="plaintext">The JSON body.</param>
        /// <returns>The base64 ciphertext to send as the request body.</returns>
        public static string EncryptPayload(string plaintext)
        {
            ArgumentNullException.ThrowIfNull(plaintext);

            using var aes = CreateAes(AesKey);
            using var encryptor = aes.CreateEncryptor();
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            return Convert.ToBase64String(encryptor.TransformFinalBlock(bytes, 0, bytes.Length));
        }

        /// <summary>
        /// Decrypts a payload that was encrypted with a key carried in an <c>encrypt-key</c> header.
        /// </summary>
        /// <param name="base64Ciphertext">The base64 response body.</param>
        /// <param name="key">The raw AES key.</param>
        /// <returns>The UTF-8 plaintext.</returns>
        public static string DecryptPayload(string base64Ciphertext, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(base64Ciphertext);
            ArgumentNullException.ThrowIfNull(key);

            using var aes = CreateAes(key);
            using var decryptor = aes.CreateDecryptor();
            var ciphertext = Convert.FromBase64String(base64Ciphertext);
            return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length));
        }

        /// <summary>
        /// Builds the <c>encrypt-key</c> header value for a request.
        /// </summary>
        /// <returns>The base64 RSA block.</returns>
        public static string CreateRequestKeyHeader()
        {
            // The client encrypts the base64 *text* of the key, not the raw key bytes.
            var encoded = Encoding.UTF8.GetBytes(Convert.ToBase64String(AesKey));

            using var rsa = RSA.Create();
            rsa.ImportFromPem(RequestPublicKeyPem);
            return Convert.ToBase64String(rsa.Encrypt(encoded, RSAEncryptionPadding.Pkcs1));
        }

        /// <summary>
        /// Unwraps a response <c>encrypt-key</c> header into the AES key it carries.
        /// </summary>
        /// <param name="headerValue">The base64 header value.</param>
        /// <returns>The raw AES key.</returns>
        /// <exception cref="CryptographicException">The header is not a valid block for the client key.</exception>
        public static byte[] UnwrapResponseKey(string headerValue)
        {
            ArgumentNullException.ThrowIfNull(headerValue);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(ResponsePrivateKeyPem);

            var encoded = rsa.Decrypt(Convert.FromBase64String(headerValue), RSAEncryptionPadding.Pkcs1);

            // The decrypted bytes are the ASCII base64 of the key, again matching the client.
            return Convert.FromBase64String(Encoding.UTF8.GetString(encoded));
        }

        private static Aes CreateAes(byte[] key)
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            return aes;
        }
    }
}
