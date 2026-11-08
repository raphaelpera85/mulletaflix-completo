using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using TMDbLib.Utilities.Serializer;

namespace MediaBrowser.Providers.Plugins.Tmdb
{
    /// <summary>
    /// Resilient JSON serializer for TMDb responses that tolerates non-standard date formats
    /// (such as "yyyy-MM-dd HH:mm:ss UTC" in video results) preventing downstream metadata/artwork drop.
    /// </summary>
    public sealed class TolerantTmdbJsonSerializer : ITMDbSerializer
    {
        private static readonly Encoding Utf8Encoding = new UTF8Encoding(false);
        private readonly JsonSerializer _serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TolerantTmdbJsonSerializer"/> class.
        /// </summary>
        public TolerantTmdbJsonSerializer()
        {
            _serializer = JsonSerializer.CreateDefault();
            _serializer.NullValueHandling = NullValueHandling.Include;
            _serializer.MissingMemberHandling = MissingMemberHandling.Ignore;
            _serializer.DateParseHandling = DateParseHandling.None;
            _serializer.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            _serializer.DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind;

            // Copy default TMDbLib converters (tagged images, search base, enums, credits, etc.)
            var defaultInstance = TMDbJsonSerializer.Instance;
            var serializerField = typeof(TMDbJsonSerializer).GetField("_serializer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (serializerField?.GetValue(defaultInstance) is JsonSerializer defaultSerializer)
            {
                foreach (var converter in defaultSerializer.Converters)
                {
                    _serializer.Converters.Add(converter);
                }
            }

            // Register tolerant date converter to handle non-standard UTC timestamps from TMDb API
            _serializer.Converters.Add(new TolerantDateTimeConverter());
        }

        /// <summary>
        /// Gets the singleton instance of <see cref="TolerantTmdbJsonSerializer"/>.
        /// </summary>
        public static TolerantTmdbJsonSerializer Instance { get; } = new TolerantTmdbJsonSerializer();

        /// <inheritdoc />
        public void Serialize(Stream target, object obj, Type type)
        {
            using var writer = new StreamWriter(target, Utf8Encoding, 1024, leaveOpen: true);
            using var jsonWriter = new JsonTextWriter(writer);
            _serializer.Serialize(jsonWriter, obj, type);
        }

        /// <inheritdoc />
        public object? Deserialize(Stream source, Type type)
        {
            using var reader = new StreamReader(source, Utf8Encoding, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
            using var jsonReader = new JsonTextReader(reader)
            {
                DateParseHandling = DateParseHandling.None
            };
            return _serializer.Deserialize(jsonReader, type);
        }

        private sealed class TolerantDateTimeConverter : JsonConverter
        {
            private static readonly string[] CustomDateFormats =
            {
                "yyyy-MM-dd HH:mm:ss 'UTC'",
                "yyyy-MM-dd HH:mm:ss UTC",
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ss.fffK",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ssK",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd"
            };

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(DateTime) || objectType == typeof(DateTime?);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return objectType == typeof(DateTime?) ? null : default(DateTime);
                }

                if (reader.TokenType == JsonToken.Date)
                {
                    if (reader.Value is DateTime dt)
                    {
                        return dt;
                    }

                    if (reader.Value is DateTimeOffset dto)
                    {
                        return dto.UtcDateTime;
                    }
                }

                if (reader.TokenType == JsonToken.String)
                {
                    var text = reader.Value as string;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return objectType == typeof(DateTime?) ? null : default(DateTime);
                    }

                    var trimmed = text.Trim();

                    if (DateTime.TryParseExact(trimmed, CustomDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var exactDate))
                    {
                        return exactDate;
                    }

                    if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offsetDate))
                    {
                        return offsetDate.UtcDateTime;
                    }

                    if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fallbackDate))
                    {
                        return fallbackDate;
                    }

                    return objectType == typeof(DateTime?) ? null : default(DateTime);
                }

                return objectType == typeof(DateTime?) ? null : default(DateTime);
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value is DateTime dt)
                {
                    writer.WriteValue(dt.ToString("O", CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteNull();
                }
            }
        }
    }
}
