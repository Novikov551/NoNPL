namespace NoNPL.Comparers;

// Сравнивает списки поэлементно в том же порядке
public class ListIntEqualityComparer : IEqualityComparer<List<int>>
{
    public bool Equals(List<int> x, List<int> y)
    {
        // Проверка на null
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        // Если количество элементов разное - списки не равны
        if (x.Count != y.Count) return false;

        // Поэлементное сравнение
        for (int i = 0; i < x.Count; i++)
        {
            if (x[i] != y[i]) return false;
        }

        return true;
    }

    public int GetHashCode(List<int> obj)
    {
        if (obj is null) return 0;

        // Комбинирование хэш-кодов элементов с учетом их порядка
        int hash = 17;
        foreach (int item in obj)
        {
            hash = hash * 31 + item.GetHashCode();
        }
        return hash;
    }
}
