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
        int maxConcurrent = 20,
        int bufferSize = 1048576,
        CancellationToken ct = default)
    {
        var splitToken = Encoding.UTF8.GetBytes(tokenSeparator);
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        var chunkData = new ConcurrentDictionary<int, string>();
        var tasks = new List<Task>();

        var stopwatch = Stopwatch.StartNew();

        AdvancedConsole.WriteLine($"Starting reading... File size: {Decimal.Round((fileSize / 1024m / 1024m), 2)} Мбайт");

        List<long> boundaries;
        using (var fileStream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            true))
        {
            boundaries = FindChunkBoundaries(
                 fileStream,
                 splitToken,
                 maxConcurrent);
        }

        var actualNumChunks = boundaries.Count - 1;
        AdvancedConsole.WriteLine($"Found {actualNumChunks} chunks");

        for (var i = 0; i < actualNumChunks; i++)
        {
            var chunkIndex = i;
            var start = boundaries[i];
            var end = boundaries[i + 1];
            var chunkLength = end - start;

            await semaphore.WaitAsync(ct);

            tasks.Add(Task.Run(async () =>
            {
                using (var fileStream = new FileStream(filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
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
                               (int)Math.Min(bufferSize, chunkLength - totalBytesRead),
                               ct)) > 0)
                    {
                        totalBytesRead += bytesRead;
                    }

                    var chunk = UTF8Converter.GetString(buffer, 0, totalBytesRead);

                    chunkData[chunkIndex] = chunk;
                }
            }));
        }

        await Task.WhenAll(tasks);

        AdvancedConsole.WriteLine($"Reading completed. Duration: {stopwatch.Elapsed:mm\\:ss\\.ff}",
            ConsoleMessageType.Success);

        stopwatch.Stop();

        return chunkData;
    }

    private static List<long> FindChunkBoundaries(
        FileStream fileStream,
        byte[] splitSpecialToken,
        int bufferSize = 1048576,
        int maxConcurrent = 20)
    {
        if (splitSpecialToken == null || splitSpecialToken.Length == 0)
            throw new ArgumentException("Специальный токен должен быть непустым массивом байт", nameof(splitSpecialToken));

        var fileSize = fileStream.Length;
        var chunkSize = fileSize / maxConcurrent;

        var chunkBoundaries = new List<long>();
        for (int i = 0; i <= maxConcurrent; i++)
        {
            chunkBoundaries.Add(i * chunkSize);
        }
        chunkBoundaries[maxConcurrent] = fileSize;

        var tokenLength = splitSpecialToken.Length;

        var chunkBufferSize = bufferSize + tokenLength - 1;

        for (var bi = 1; bi < chunkBoundaries.Count - 1; bi++)
        {
            var initialPosition = chunkBoundaries[bi];
            var currentPosition = initialPosition;
            var previousRemaining = Array.Empty<byte>();

            while (true)
            {
                fileStream.Seek(currentPosition, SeekOrigin.Begin);
                var buffer = new byte[chunkBufferSize];
                var bytesRead = fileStream.Read(buffer, 0, chunkBufferSize);

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

                currentPosition += bufferSize;

                if (currentPosition >= fileSize)
                {
                    chunkBoundaries[bi] = fileSize;
                    break;
                }
            }
        }

        return chunkBoundaries.Distinct().OrderBy(x => x).ToList();
    }

    private static int FindSequence(byte[] source,
        byte[] pattern,
        int sourceLength)
    {
        if (pattern.Length == 0) return 0;
        if (pattern.Length > sourceLength) return -1;

        for (var i = 0; i <= sourceLength - pattern.Length; i++)
        {
            bool found = true;
            for (var j = 0; j < pattern.Length; j++)
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