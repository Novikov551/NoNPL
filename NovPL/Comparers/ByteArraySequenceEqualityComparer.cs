namespace NoNPL.Comparers
{
    public class ByteArraySequenceEqualityComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArraySequenceEqualityComparer Instance = new();

        public bool Equals(byte[] x, byte[] y)
        {
            // Быстрая проверка ссылок
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            // Оптимизация: если длины разные, массивы точно не равны
            if (x.Length != y.Length) return false;

            // Побайтовое сравнение
            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            if (obj is null || obj.Length == 0) return 0;

            // Простая и быстрая реализация хэш-кода
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < obj.Length; i++)
                {
                    hash = hash * 31 + obj[i];
                }
                return hash;
            }
        }
    }
}
