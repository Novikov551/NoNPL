using System;
using System.Buffers;
using System.Text;

namespace NoNPL.Entities
{
    public static class UTF8Converter
    {
        private static readonly Encoding _utf8 = new UTF8Encoding(false);

        public static string GetString(ReadOnlySpan<byte> bytes)
        {
            return _utf8.GetString(bytes);
        }

        public static byte[] GetBytes(ReadOnlySpan<char> chars)
        {
            chars = StripBom(chars);
            int byteCount = _utf8.GetByteCount(chars);
            byte[] bytes = new byte[byteCount];
            _utf8.GetBytes(chars, bytes);
            return bytes;
        }

        public static int GetCharCount(ReadOnlySpan<byte> bytes)
        {
            return _utf8.GetCharCount(bytes);
        }

        public static int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            return _utf8.GetChars(bytes, chars);
        }

        public static DecodedSpan DecodeToSpan(ReadOnlySpan<byte> bytes)
        {
            int charCount = _utf8.GetCharCount(bytes);
            char[] rented = ArrayPool<char>.Shared.Rent(charCount);
            int written = _utf8.GetChars(bytes, rented);
            return new DecodedSpan(rented, written);
        }

        public ref struct DecodedSpan
        {
            private readonly char[] _rented;
            private readonly int _length;

            internal DecodedSpan(char[] rented, int length)
            {
                _rented = rented;
                _length = length;
            }

            public ReadOnlySpan<char> Span => _rented.AsSpan(0, _length);

            public void Dispose()
            {
                if (_rented != null)
                    ArrayPool<char>.Shared.Return(_rented);
            }
        }

        private static ReadOnlySpan<char> StripBom(ReadOnlySpan<char> span)
        {
            if (span.Length > 0 && span[0] == '\uFEFF')
                return span.Slice(1);
            return span;
        }
    }
}
