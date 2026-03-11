using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NoNPL.DataReaders;

public class TXTDatasetReader
{
    public async IAsyncEnumerable<byte[]> ReadTXTDatasetAsync(
        string filePath,
        byte[] splitToken,
        long bufferSize = 100 * 1024 * 1024,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        if (splitToken == null || splitToken.Length == 0)
            throw new ArgumentException("Token bytes cannot be null or empty.", nameof(splitToken));
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive.");

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        byte[]? leftover = null; // Данные, не возвращённые из предыдущих итераций
        int leftoverLength = 0;
        int tokenLen = splitToken.Length;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Читаем очередной блок
            byte[] buffer = new byte[bufferSize];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                         .ConfigureAwait(false);

            // Пытаемся найти последнее вхождение токена в комбинации leftover + buffer
            int posInCombined = FindLastTokenPosition(leftover, leftoverLength, buffer, bytesRead, splitToken);

            if (posInCombined >= 0)
            {
                // Токен найден – формируем фрагмент и остаток
                int endPos = posInCombined + tokenLen;  // индекс первого байта после токена

                // Фрагмент: все байты от начала объединения до endPos
                byte[] fragment = new byte[endPos];
                CopyCombinedData(leftover, leftoverLength, buffer, bytesRead, 0, fragment, endPos);
                yield return fragment;

                // Остаток: данные после endPos
                int remainingTotal = leftoverLength + bytesRead - endPos;
                if (remainingTotal > 0)
                {
                    byte[] newLeftover = new byte[remainingTotal];
                    CopyCombinedData(leftover, leftoverLength, buffer, bytesRead, endPos, newLeftover, remainingTotal);
                    leftover = newLeftover;
                    leftoverLength = remainingTotal;
                }
                else
                {
                    leftover = null;
                    leftoverLength = 0;
                }
            }
            else
            {
                // Токен не найден
                if (bytesRead < bufferSize) // Конец файла
                {
                    // Возвращаем всё накопленное как последний фрагмент
                    int total = leftoverLength + bytesRead;
                    if (total > 0)
                    {
                        byte[] fragment = new byte[total];
                        CopyCombinedData(leftover, leftoverLength, buffer, bytesRead, 0, fragment, total);
                        yield return fragment;
                    }
                    yield break;
                }
                else
                {
                    // Сохраняем всё для следующего чтения
                    int total = leftoverLength + bytesRead;
                    byte[] newLeftover = new byte[total];
                    CopyCombinedData(leftover, leftoverLength, buffer, bytesRead, 0, newLeftover, total);
                    leftover = newLeftover;
                    leftoverLength = total;
                }
            }
        }
    }

    // Поиск последнего вхождения токена в объединении двух массивов (leftover + buffer)
    private int FindLastTokenPosition(byte[]? leftover, int leftoverLen, byte[] buffer, int bufferLen, byte[] token)
    {
        int tokenLen = token.Length;

        // 1. Ищем в buffer (полное вхождение)
        int posInBuffer = FindLastIndexOf(buffer, 0, bufferLen, token);
        if (posInBuffer >= 0)
            return leftoverLen + posInBuffer;

        // 2. Ищем пересекающее вхождение (начало в leftover, конец в buffer)
        if (leftoverLen > 0)
        {
            int startCheck = Math.Max(0, leftoverLen - tokenLen + 1);
            for (int i = leftoverLen - 1; i >= startCheck; i--)
            {
                int needFromBuffer = tokenLen - (leftoverLen - i);
                if (needFromBuffer <= bufferLen && needFromBuffer > 0)
                {
                    bool match = true;
                    // Проверяем часть из leftover
                    for (int j = 0; j < leftoverLen - i; j++)
                    {
                        if (leftover[i + j] != token[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        // Проверяем часть из buffer
                        for (int j = 0; j < needFromBuffer; j++)
                        {
                            if (buffer[j] != token[(leftoverLen - i) + j])
                            {
                                match = false;
                                break;
                            }
                        }
                    }
                    if (match)
                        return i; // позиция начала токена в leftover
                }
            }
        }

        // 3. Ищем полное вхождение в leftover
        if (leftoverLen > 0)
            return FindLastIndexOf(leftover, 0, leftoverLen, token);

        return -1;
    }

    // Поиск последнего вхождения подмассива в заданном диапазоне массива (наивный алгоритм)
    private int FindLastIndexOf(byte[] data, int start, int length, byte[] token)
    {
        if (length < token.Length) return -1;
        int lastPossible = start + length - token.Length;
        for (int i = lastPossible; i >= start; i--)
        {
            bool found = true;
            for (int j = 0; j < token.Length; j++)
            {
                if (data[i + j] != token[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }

    // Копирование части объединённых данных (leftover + buffer) в целевой массив,
    // начиная с объединённого индекса sourceOffset и длиной count байт.
    private void CopyCombinedData(byte[]? leftover, int leftoverLen, byte[] buffer, int bufferLen,
                                         int sourceOffset, byte[] destination, int count)
    {
        int copied = 0;
        if (sourceOffset < leftoverLen)
        {
            int fromLeftover = Math.Min(leftoverLen - sourceOffset, count);
            Buffer.BlockCopy(leftover!, sourceOffset, destination, 0, fromLeftover);
            copied = fromLeftover;
            sourceOffset += fromLeftover;
        }
        if (copied < count && sourceOffset >= leftoverLen)
        {
            int bufferOffset = sourceOffset - leftoverLen;
            int fromBuffer = Math.Min(bufferLen - bufferOffset, count - copied);
            Buffer.BlockCopy(buffer, bufferOffset, destination, copied, fromBuffer);
        }
    }
}