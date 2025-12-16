namespace NoNPL.Comparers;

// Компаратор для кортежей массивов байтов
public class ByteArrayTupleEqualityComparer : IEqualityComparer<(byte[], byte[])>
{
    public bool Equals((byte[], byte[]) x, (byte[], byte[]) y)
    {
        // Сравниваем оба массива попарно
        return ByteArraysEqual(x.Item1, y.Item1) && ByteArraysEqual(x.Item2, y.Item2);
    }

    private bool ByteArraysEqual(byte[] a, byte[] b)
    {
        // Проверка на null
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // Проверка длины
        if (a.Length != b.Length) return false;

        // Побайтовое сравнение
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }

    public int GetHashCode((byte[], byte[]) obj)
    {
        // Используем комбинированный хеш-код
        int hash = 17;

        // Хеш-код первого массива
        if (obj.Item1 != null)
        {
            foreach (byte b in obj.Item1)
            {
                hash = hash * 31 + b.GetHashCode();
            }
        }

        // Разделитель между двумя массивами
        hash = hash * 31 + 0xBEEF;

        // Хеш-код второго массива
        if (obj.Item2 != null)
        {
            foreach (byte b in obj.Item2)
            {
                hash = hash * 31 + b.GetHashCode();
            }
        }

        return hash;
    }
}
