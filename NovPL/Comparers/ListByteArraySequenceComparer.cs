namespace NoNPL.Comparers;

public class ListByteArraySequenceComparer : IEqualityComparer<List<byte[]>>
{
    public static readonly ListByteArraySequenceComparer Instance = new();

    public bool Equals(List<byte[]> x, List<byte[]> y)
    {
        // Проверка ссылок
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        // Разное количество элементов
        if (x.Count != y.Count) return false;

        // Поэлементное сравнение с учетом порядка
        for (int i = 0; i < x.Count; i++)
        {
            if (!AreArraysEqual(x[i], y[i]))
                return false;
        }

        return true;
    }

    public int GetHashCode(List<byte[]> obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            int hash = 17;
            foreach (byte[] array in obj)
            {
                // Умножаем на простое число и добавляем хэш массива
                hash = hash * 31 + GetArrayHashCode(array);
            }
            return hash;
        }
    }

    private static bool AreArraysEqual(byte[] x, byte[] y)
    {
        // Оба null или обе пустые ссылки
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Length != y.Length) return false;

        // Быстрое сравнение через Span
        return x.AsSpan().SequenceEqual(y);
    }

    private static int GetArrayHashCode(byte[] array)
    {
        if (array is null) return 0;

        // Простой детерминированный хэш для массива байтов
        unchecked
        {
            int hash = 17;
            foreach (byte b in array)
            {
                hash = hash * 31 + b;
            }
            return hash;
        }
    }
}
