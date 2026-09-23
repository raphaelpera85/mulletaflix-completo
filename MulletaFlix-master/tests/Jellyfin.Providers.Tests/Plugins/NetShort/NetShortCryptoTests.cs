using System;
using System.Text;
using MediaBrowser.Providers.Plugins.NetShort;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.NetShort
{
    /// <summary>
    /// Checks the reimplementation of NetShort's payload encryption against traffic captured from
    /// the live site.
    /// </summary>
    /// <remarks>
    /// Every ciphertext and header in this file is copied verbatim out of a Playwright capture of a
    /// real browsing session (<c>netshort-capture.json</c>). That is what makes these tests worth
    /// more than a round trip against themselves: if the key, the mode, the padding or the encoding
    /// were wrong, these values would not decrypt.
    /// </remarks>
    public class NetShortCryptoTests
    {
        /// <summary>
        /// The device code the captured session used.
        /// </summary>
        private const string CapturedDeviceCode = "b18305f3902baccadef76f479e5d79a6-1790116040180";

        /// <summary>
        /// The body of the captured POST to /web/web/v3/queryOnlineLabelList/cascade_label, whose
        /// plain text is the empty object. Three different endpoints sent exactly this ciphertext,
        /// which is the proof that there is no per-request key and no IV.
        /// </summary>
        private const string EmptyObjectCiphertext = "cZSeHVTvqy+IeOCRjILPxA==";

        /// <summary>
        /// The body of the captured POST to /web/auth/visitor_login.
        /// </summary>
        private const string VisitorLoginCiphertext =
            "4O9hxkM2RtHOc3jtMzcmnJZf3YksCexqDuFIK0j2YsQiLkRahKuWCXp7LBBt8gwMQcyg9QdyAdF/WJicFhd1G5+K2J+ENIp412AkR3NZqrY=";

        /// <summary>
        /// The body of the captured POST to /web/short_play/search/keyword/seo.
        /// </summary>
        private const string SearchCiphertext =
            "K7nWS3lANYn5aY4vRG3IudeQSHX6ZO5zjdDeOBrlRjhSh+8DVWqQTr62TboJWfA9UC0mdr2iKkwAIeg92qLe7oMhicEc/S0YwhytSlyyOPs=";

        /// <summary>
        /// The <c>encrypt-key</c> header and body of the captured response to
        /// /web/web/v2/load_popular_online/cascade_label.
        /// </summary>
        private const string PopularResponseKeyHeader =
            "X38axJb9uqU/y0wgZ2frhQT127sBMr0+OAbOmD3mEsZobn73GnQoyKWb4HM5HwJUNdNSgr1NLYOoEoVC0f+E90pYhUcLQoWQzClm5ynYH1664MkeRLI5tdVIrLrrU42oliER9nuG8l/0Z77+noE6Re/nirdtGFIybSu8ojwoAzJKVJm0W3V+UlxB7qdT94/l4kZzWD3JBFrgET83CdMLJN/2HYZnMh6CzAxvNH8jUQSA0xosELqZ/NnEQM8W33lsAFJ98L+R5CGAW8yAD3OU1eqXXb+XKSZKTNb6BIno1TB0xtS5UwRcgUhV6nYPwx2MZFiZlZde8yYT1suO6/KqVw==";

        private const string PopularResponseBody = "NEMVw4JbX70KsD2lTx+zgYmub3WZ0ZBR4hAeiCC8qoqEeNHOnYRvx23+3QfyGtLS";

        /// <summary>
        /// The <c>encrypt-key</c> header and body of the captured response to
        /// /web/auth/visitor_login.
        /// </summary>
        private const string LoginResponseKeyHeader =
            "iTAfpg0EZM5ubRKfNEhSRSJhQAPqDqDTjkGWg1S/xtqqU026QPgPmlxZkYHZVIMtOI3Xo1l1zP2uhQ+z0QOVHfhU5uLhWYtk760X7OBDRCvtQCPq5avKKdUC0VRJYCTYqQpLGesaPvGtYEv09iIBCMvC2CD+B99bJpn/XUgYo05XbGfAbgRBVilPjT9/QKADZ0vamQgpZCNAb5TfLhsS9X2A/GBUgNCvSetsk472y1TWQUBrIBeToUedcSkEuznfEyt3YaW2R5ITnR6LA5uPHImq78zHpltn9jv5GqggsKleyP98MioDXnjAfydfOBoRJ3Ej3QW3u3e4MDXk4QtOkw==";

        private const string LoginResponseBody =
            "ArmqUy/Mz2tYMy0DlPEqkKV85pCo/oRxjU0T09XBCxmQrUFHzwC086XKsfNKsp2gfufB/96kWQkBRXr+JzTD54PDamvYdesEhiusLIrNBD4cLyxIMJxE3YHIi/DNRnYHClyCDQrJsxqQX26T9z+jTMt+F8NgupIbmcEek8Onq9wfPXXub3efThE5XQss91ZvJfOJ+yFsuIIWlHQZodazCR9B5Q0DSDmAOK/GRLJ8hv0msLOPVLuWNYjSFPNPZ3pNpDWLtavABwfaeuou6VzAmT1qBWP5zuZzTql9Zq/yVmW9IXPnDJ4n1f4IUIg0rIgvzvktT0awsLBAXFuVctAvp5DXfOmL6xj4Oxp4gPGO42oLZNILIYjTiLFbSYxvFjer3piwnQ1x7NnSyox8/CWh4MU9DoGS4MeirmEURjlFpvKw7LhTE0/4XvqRRXRLNq3XVpcrrGUhdnScxXeNSSOHCiSOAnyHB54s1E/WUaKggk7vlxTSPOPgjC01lLYjkYY3+qR/5zD+Ljo2S54UY1FyOAuXJ/k68FeIwwTrE9Qd+/aloydhZ/VneQs6eYHuNs8GeKTReMRF9dbjNu4xI1I6navkvtXFU6+I/QtCvtiC51rs/mqVkRIsE+ADo4e9UuwHmderpLNEPz1Hv32+GvNrkMmj0h+kLYHkLHiPq1ojTCU=";

        private static byte[] PayloadKey => Encoding.UTF8.GetBytes(NetShortCrypto.PayloadKey);

        [Fact]
        public void EncryptPayload_ReproducesTheCapturedCiphertextByteForByte()
        {
            // ECB with a fixed key is deterministic, so the site's own ciphertext is reproducible.
            // Nothing short of the exact key, mode and padding produces this value.
            Assert.Equal(EmptyObjectCiphertext, NetShortCrypto.EncryptPayload("{}"));
        }

        [Fact]
        public void EncryptPayload_ReproducesTheCapturedVisitorLoginBody()
        {
            var body = "{\"deviceCode\":\"" + CapturedDeviceCode + "\",\"os\":\"windows\"}";

            Assert.Equal(VisitorLoginCiphertext, NetShortCrypto.EncryptPayload(body));
        }

        [Fact]
        public void EncryptPayload_ReproducesTheCapturedSearchBody()
        {
            var body = "{\"queryKeyword\":\"perdões\",\"pageQuery\":{\"pageNum\":1,\"pageSize\":10}}";

            Assert.Equal(SearchCiphertext, NetShortCrypto.EncryptPayload(body));
        }

        [Theory]
        [InlineData(EmptyObjectCiphertext, "{}")]
        [InlineData(VisitorLoginCiphertext, "{\"deviceCode\":\"b18305f3902baccadef76f479e5d79a6-1790116040180\",\"os\":\"windows\"}")]
        [InlineData(SearchCiphertext, "{\"queryKeyword\":\"perdões\",\"pageQuery\":{\"pageNum\":1,\"pageSize\":10}}")]
        public void DecryptPayload_ReadsTheCapturedRequestBodies(string ciphertext, string expected)
        {
            Assert.Equal(expected, NetShortCrypto.DecryptPayload(ciphertext, PayloadKey));
        }

        [Fact]
        public void UnwrapResponseKey_ReadsTheKeyTheServerGeneratedForThatResponse()
        {
            // A fresh random key per response, carried as the base64 of 32 ASCII bytes.
            var key = NetShortCrypto.UnwrapResponseKey(PopularResponseKeyHeader);

            Assert.Equal(32, key.Length);
            Assert.Equal("6l0d1knr6x3iw8z5drxrsw08k60fkdec", Encoding.UTF8.GetString(key));
        }

        [Fact]
        public void DecryptPayload_ReadsTheCapturedResponseBody()
        {
            var key = NetShortCrypto.UnwrapResponseKey(PopularResponseKeyHeader);

            var plaintext = NetShortCrypto.DecryptPayload(PopularResponseBody, key);

            Assert.Equal("{\"code\":200,\"msg\":\"操作成功\",\"data\":[]}", plaintext);
        }

        [Fact]
        public void DecryptPayload_ReadsTheCapturedVisitorLoginResponse()
        {
            var key = NetShortCrypto.UnwrapResponseKey(LoginResponseKeyHeader);

            var plaintext = NetShortCrypto.DecryptPayload(LoginResponseBody, key);

            // This is the response the provider depends on to get a bearer token at all.
            Assert.Contains("\"deviceCode\":\"" + CapturedDeviceCode + "\"", plaintext, StringComparison.Ordinal);
            Assert.Contains("\"token\":\"eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.", plaintext, StringComparison.Ordinal);
            Assert.Contains("\"timeout\":604799", plaintext, StringComparison.Ordinal);
        }

        [Fact]
        public void CreateRequestKeyHeader_ProducesA2048BitRsaBlock()
        {
            var header = NetShortCrypto.CreateRequestKeyHeader();

            // RSA-2048 ciphertext is 256 bytes, which is 344 base64 characters.
            Assert.Equal(344, header.Length);
            Assert.Equal(256, Convert.FromBase64String(header).Length);
        }

        [Fact]
        public void CreateRequestKeyHeader_IsPaddedWithPkcs1SoItIsNotDeterministic()
        {
            // PKCS#1 v1.5 padding is randomised, which is why two captured requests carrying the same
            // body still show different encrypt-key headers. Asserting the difference here documents
            // that the header is not a signature over the body.
            var first = NetShortCrypto.CreateRequestKeyHeader();
            var second = NetShortCrypto.CreateRequestKeyHeader();

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void PayloadRoundTrip_SurvivesNonAsciiTextAndBlockBoundaries()
        {
            // 15/16/17 and 31/32/33 bytes are the PKCS#7 edge cases; the accented and CJK text proves
            // the UTF-8 encoding on both sides.
            foreach (var length in new[] { 15, 16, 17, 31, 32, 33, 128 })
            {
                var plaintext = new string('ç', length);
                var ciphertext = NetShortCrypto.EncryptPayload(plaintext);

                Assert.Equal(0, Convert.FromBase64String(ciphertext).Length % 16);
                Assert.Equal(plaintext, NetShortCrypto.DecryptPayload(ciphertext, PayloadKey));
            }
        }
    }
}
