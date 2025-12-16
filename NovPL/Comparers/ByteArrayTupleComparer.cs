namespace NoNPL.Comparers;

public class ByteArrayTupleComparer : IComparer<(byte[], byte[])>
{
    public int Compare((byte[], byte[]) x, (byte[], byte[]) y)
    {
        // Сначала сравниваем по длине первого массива
        int firstLengthComparison = x.Item1.Length.CompareTo(y.Item1.Length);
        if (firstLengthComparison != 0)
            return firstLengthComparison;

        // Затем побайтовое сравнение первого массива
        for (int i = 0; i < x.Item1.Length; i++)
        {
            int byteComparison = x.Item1[i].CompareTo(y.Item1[i]);
            if (byteComparison != 0)
                return byteComparison;
        }

        // Если первые массивы равны, сравниваем вторые массивы
        int secondLengthComparison = x.Item2.Length.CompareTo(y.Item2.Length);
        if (secondLengthComparison != 0)
            return secondLengthComparison;

        for (int i = 0; i < x.Item2.Length; i++)
        {
            int byteComparison = x.Item2[i].CompareTo(y.Item2[i]);
            if (byteComparison != 0)
                return byteComparison;
        }

        return 0; // Оба кортежа равны
    }
}
