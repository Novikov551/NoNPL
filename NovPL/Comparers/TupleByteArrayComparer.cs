namespace NoNPL.Comparers;

public class TupleByteArrayComparer : IEqualityComparer<Tuple<byte[], byte[]>>
{
    public bool Equals(Tuple<byte[], byte[]> x, Tuple<byte[], byte[]> y)
    {
        // Сравнение ссылок
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        // Сравнение первого массива
        if (!AreArraysEqual(x.Item1, y.Item1))
            return false;

        // Сравнение второго массива
        return AreArraysEqual(x.Item2, y.Item2);
    }

    public int GetHashCode(Tuple<byte[], byte[]> obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            int hash = 17;

            // Хэш первого массива
            if (obj.Item1 != null)
            {
                foreach (byte b in obj.Item1)
                {
                    hash = hash * 31 + b;
                }
            }
            else
            {
                hash = hash * 31;
            }

            // Разделитель между массивами (чтобы (A,B) ≠ (B,A))
            hash = (int)(hash * 31 + 0xDEADBEEF);

            // Хэш второго массива
            if (obj.Item2 != null)
            {
                foreach (byte b in obj.Item2)
                {
                    hash = hash * 31 + b;
                }
            }

            return hash;
        }
    }

    private static bool AreArraysEqual(byte[] x, byte[] y)
    {
        // Оба null или оба пусты
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Length != y.Length) return false;

        // Побайтовое сравнение
        return x.AsSpan().SequenceEqual(y);
    }
}
