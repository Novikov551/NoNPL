namespace NoNPL.Comparers;

public class ListStringSequenceComparer : IEqualityComparer<List<string>>
{
    public static readonly ListStringSequenceComparer Instance = new();

    public bool Equals(List<string> x, List<string> y)
    {
        // Проверка ссылок
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        // Если количество разное - не равны
        if (x.Count != y.Count) return false;

        // Поэлементное сравнение с учетом порядка
        for (int i = 0; i < x.Count; i++)
        {
            // StringComparison.Ordinal для точного сравнения
            if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public int GetHashCode(List<string> obj)
    {
        if (obj is null) return 0;

        unchecked
        {
            int hash = 17;
            foreach (string item in obj)
            {
                // Учитываем null-элементы
                hash = hash * 31 + (item?.GetHashCode(StringComparison.Ordinal) ?? 0);
            }
            return hash;
        }
    }
}
