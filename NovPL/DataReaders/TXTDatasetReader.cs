using NoNPL.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
namespace NoNPL.DataReaders;

public class TXTDatasetReader
{
    public async Task<ConcurrentDictionary<int, string>> ReadTXTDatasetAsync(
        string filePath,
        string tokenSeparator,
        int maxConcurrent = 12)
    {
        var splitToken = Encoding.UTF8.GetBytes(tokenSeparator);
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        var chunkData = new ConcurrentDictionary<int, string>();
        var tasks = new List<Task>();

        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine($"Начало чтения файла. Размер файла: {fileSize} байт");

        List<long> boundaries;
        using (var fileStream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1048576,
            true))
        {
            boundaries = FindChunkBoundaries(
                 fileStream,
                 splitToken,
                 maxConcurrent);
        }

        var actualNumChunks = boundaries.Count - 1;
        Console.WriteLine($"Найдено {actualNumChunks} чанков");

        for (var i = 0; i < actualNumChunks; i++)
        {
            var chunkIndex = i;
            var start = boundaries[i];
            var end = boundaries[i + 1];
            var chunkLength = end - start;

            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                using (var fileStream = new FileStream(filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1048576,
                    true))
                {
                    // Создаем буфер для чанка
                    var buffer = new byte[chunkLength];

                    // Перемещаемся к началу чанка
                    fileStream.Seek(start, SeekOrigin.Begin);

                    // Читаем весь чанк
                    var totalBytesRead = 0;
                    int bytesRead;

                    // Читаем частями на случай большого чанка
                    while (totalBytesRead < chunkLength &&
                           (bytesRead = await fileStream.ReadAsync(
                               buffer,
                               totalBytesRead,
                               (int)Math.Min(1048576, chunkLength - totalBytesRead))) > 0)
                    {
                        totalBytesRead += bytesRead;
                    }

                    var chunk = UTF8Converter.GetString(buffer, 0, totalBytesRead);
                    
                    chunkData[chunkIndex] = chunk;

                    Console.WriteLine($"Чанк {chunkIndex}: {start}-{end} байт, размер: {chunkLength} байт, прочитано: {totalBytesRead} байт");
                }
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine($"\nЧтение завершено за {stopwatch.Elapsed:mm\\:ss\\.ff}");
        Console.WriteLine($"Прочитано чанков: {chunkData.Count}");

        stopwatch.Stop();

        return chunkData;
    }

    private static List<long> FindChunkBoundaries(
        FileStream fileStream,
        byte[] splitSpecialToken,
        int maxConcurrent = 4)
    {
        if (splitSpecialToken == null || splitSpecialToken.Length == 0)
            throw new ArgumentException("Специальный токен должен быть непустым массивом байт", nameof(splitSpecialToken));

        long fileSize = fileStream.Length;
        long chunkSize = fileSize / maxConcurrent;

        var chunkBoundaries = new List<long>();
        for (int i = 0; i <= maxConcurrent; i++)
        {
            chunkBoundaries.Add(i * chunkSize);
        }
        chunkBoundaries[maxConcurrent] = fileSize;

        const int miniChunkSize = 1048576;
        var tokenLength = splitSpecialToken.Length;

        var bufferSize = miniChunkSize + tokenLength - 1;

        for (var bi = 1; bi < chunkBoundaries.Count - 1; bi++)
        {
            var initialPosition = chunkBoundaries[bi];
            var currentPosition = initialPosition;
            var previousRemaining = Array.Empty<byte>();

            while (true)
            {
                fileStream.Seek(currentPosition, SeekOrigin.Begin);
                var buffer = new byte[bufferSize];
                var bytesRead = fileStream.Read(buffer, 0, bufferSize);

                if (bytesRead == 0)
                {
                    chunkBoundaries[bi] = fileSize;
                    break;
                }

                byte[] searchBuffer;
                if (previousRemaining.Length > 0)
                {
                    searchBuffer = new byte[previousRemaining.Length + bytesRead];
                    Array.Copy(previousRemaining, 0, searchBuffer, 0, previousRemaining.Length);
                    Array.Copy(buffer, 0, searchBuffer, previousRemaining.Length, bytesRead);
                }
                else
                {
                    searchBuffer = new byte[bytesRead];
                    Array.Copy(buffer, 0, searchBuffer, 0, bytesRead);
                }

                var foundIndex = FindSequence(searchBuffer, splitSpecialToken, searchBuffer.Length);
                if (foundIndex != -1)
                {
                    chunkBoundaries[bi] = currentPosition - previousRemaining.Length + foundIndex;
                    break;
                }

                var keepBytes = Math.Min(tokenLength - 1, bytesRead);
                previousRemaining = new byte[keepBytes];
                Array.Copy(buffer, bytesRead - keepBytes, previousRemaining, 0, keepBytes);

                currentPosition += miniChunkSize;

                if (currentPosition >= fileSize)
                {
                    chunkBoundaries[bi] = fileSize;
                    break;
                }
            }
        }

        return chunkBoundaries.Distinct().OrderBy(x => x).ToList();
    }

    private static int FindSequence(byte[] source, byte[] pattern, int sourceLength)
    {
        if (pattern.Length == 0) return 0;
        if (pattern.Length > sourceLength) return -1;

        for (int i = 0; i <= sourceLength - pattern.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
}