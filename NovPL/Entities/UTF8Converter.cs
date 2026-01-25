using System.Text;

namespace NoNPL.Entities
{
    public static class UTF8Converter
    {
        private static readonly Encoding _utf8WithoutBOM = new UTF8Encoding(false);

        public static string GetString(byte[] buffer, int index, int totalBytesRead)
        {
            var str = _utf8WithoutBOM.GetString(buffer, index, totalBytesRead);

            // Быстрая проверка на BOM
            if (str.Length > 0 && str[0] == '\uFEFF')
            {
                return str.Substring(1);
            }

            return str;
        }
    }
}
