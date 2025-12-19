using NoNPL.Comparers;
using NoNPL.DataReaders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace NoNPL;

public class BPETokenizer : IBPETokenizer
{
    private ConcurrentDictionary<int, byte[]> _vocab;
    private ConcurrentDictionary<byte[], int> _reversedVocab;
    private List<(byte[], byte[])> _merges = new List<(byte[], byte[])>();
    private ConcurrentDictionary<List<byte[]>, int> _preTokens;
    private ConcurrentDictionary<string, int> _strPreTokens;
    //Для дебага
    private ConcurrentDictionary<int, string> _strVocab;

    private readonly Regex _pattern;

    private readonly ByteArraySequenceEqualityComparer _byteArraySequenceEqualityComparer;
    private readonly ListByteArraySequenceComparer _listByteArraySequenceComparer;
    private readonly ByteArrayTupleEqualityComparer _byteArrayTupleEqualityComparer;
    private readonly ByteArrayTupleComparer _byteArrayTupleComparer;

    private readonly int _vocabSize;
    private readonly int _numMerges;

    public BPETokenizer(string pattern, int vocabSize, int numMerges)
    {
        _vocabSize = vocabSize;
        _numMerges = numMerges;

        _byteArraySequenceEqualityComparer = new();
        _listByteArraySequenceComparer = new();
        _byteArrayTupleEqualityComparer = new();
        _byteArrayTupleComparer = new();

        _pattern = new Regex(pattern, RegexOptions.Compiled);
        _vocab = new();
        _reversedVocab = new(_byteArraySequenceEqualityComparer);
        _strVocab = new();
        _merges = new();
        _preTokens = new(_listByteArraySequenceComparer);
        _strPreTokens = new();
    }

    public async Task Train(string filePath, string tokenSeparator, int maxConcurrent = 4)
    {
        InitVocabularity([tokenSeparator]);

        var chunks = await ReadTXTDatasetAsync(filePath, tokenSeparator, maxConcurrent);

        ProcessChunks(chunks, tokenSeparator);
        //Делаем слияния
        /* MergePairs();*/
    }

    private void ProcessChunks(ConcurrentDictionary<int, string> chunks, string tokenSeparator)
    {
        var stopwatch = Stopwatch.StartNew();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount 
        };

        Parallel.ForEach(chunks, options, chunk =>
        {
            var blocks = chunk.Value.Split(tokenSeparator);
            foreach (var block in blocks)
            {
                Pretokenize(block);
            }
        });

        Console.WriteLine($"Обработано за {stopwatch.Elapsed:mm\\:ss\\.ff}");

        stopwatch.Stop();
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
        int tokenLength = splitSpecialToken.Length;

        int bufferSize = miniChunkSize + tokenLength - 1;

        for (int bi = 1; bi < chunkBoundaries.Count - 1; bi++)
        {
            long initialPosition = chunkBoundaries[bi];
            long currentPosition = initialPosition;
            byte[] previousRemaining = Array.Empty<byte>();

            while (true)
            {
                fileStream.Seek(currentPosition, SeekOrigin.Begin);
                byte[] buffer = new byte[bufferSize];
                int bytesRead = fileStream.Read(buffer, 0, bufferSize);

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

                int foundIndex = FindSequence(searchBuffer, splitSpecialToken, searchBuffer.Length);
                if (foundIndex != -1)
                {
                    chunkBoundaries[bi] = currentPosition - previousRemaining.Length + foundIndex;
                    break;
                }

                int keepBytes = Math.Min(tokenLength - 1, bytesRead);
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

    /*private async Task<ConcurrentDictionary<int, (byte[] buffer, int bytesRead)>> ReadTXTDatasetAsync(
        string filePath,
        string tokenSeparator,
        int maxConcurrent = 4,
        int chunkSize = 4096)
    {
        var splitToken = Encoding.UTF8.GetBytes(tokenSeparator);
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
        var chunkData = new ConcurrentDictionary<int, (byte[] buffer, int bytesRead)>();
        var tasks = new List<Task>();

        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine($"Начало чтения файла. Всего чанков: {totalChunks}");

        for (int i = 0; i < totalChunks; i++)
        {
            int chunkIndex = i;

            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var startPosition = chunkIndex * (long)chunkSize;
                    var currentChunkSize = (int)Math.Min(chunkSize, fileSize - startPosition);

                    using (var fileStream = new FileStream(filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        4096,
                        true))
                    {
                        var buffer = new byte[currentChunkSize];
                        fileStream.Seek(startPosition, SeekOrigin.Begin);
                        var bytesRead = await fileStream.ReadAsync(buffer, 0, currentChunkSize);

                        chunkData[chunkIndex] = (buffer, bytesRead);
                        *//*var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        int lastSeparatorIndex = chunk.LastIndexOf(tokenSeparator, StringComparison.Ordinal);
                        if (lastSeparatorIndex != -1)
                        {
                            string mainPart = chunk.Substring(0, lastSeparatorIndex + tokenSeparator.Length);
                            string tail = chunk.Substring(lastSeparatorIndex + tokenSeparator.Length);
                            chunkData[chunkIndex] = (mainPart, tail);
                        }
                        else
                        {
                            chunkData[chunkIndex] = (chunk, string.Empty);
                        }*//*
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine($"\nЧтение завершено за {stopwatch.Elapsed:mm\\:ss\\.ff} | ");

        stopwatch.Stop();

        return chunkData;
    }*/

    /* private async Task<ConcurrentDictionary<int, string>> ProcessChunksAsync(
         ConcurrentDictionary<int, (string mainPart, string tail)> chunkData,
         string tokenSeparator)
     {
         Console.WriteLine($"Запуск претокенизации...");
         var stopwatch = Stopwatch.StartNew();
         var finalChunks = new ConcurrentDictionary<int, string>();
         var sortedKeys = chunkData.Keys.OrderBy(k => k).ToArray();

         Parallel.ForEach(sortedKeys, async (chunkIndex, cancellationToken) =>
         {
             var current = chunkData[chunkIndex];

             if (chunkIndex == 0)
             {
                 finalChunks[chunkIndex] = current.mainPart;
                 ProcessChunk(finalChunks[chunkIndex], tokenSeparator);
             }
             else
             {
                 if (chunkData.TryGetValue(chunkIndex - 1, out var previous))
                 {
                     finalChunks[chunkIndex] = previous.tail + current.mainPart;
                 }
                 else
                 {
                     finalChunks[chunkIndex] = current.mainPart;
                 }
                 ProcessChunk(finalChunks[chunkIndex], tokenSeparator);
             }
         });

         stopwatch.Stop();
         Console.WriteLine($"Претокенизация завершена за {stopwatch.Elapsed.TotalSeconds:F2} секунд");

         return finalChunks;
     }*/

    /*private void ProcessChunk(string chunk, string tokenSeparator)
    {
        var chunks = chunk.Split([tokenSeparator], StringSplitOptions.None);
        foreach (var text in chunks)
        {
            PreTokenize(text);
        }
    }*/

    /*private void MergePairs()
    {
        var stopwatch = Stopwatch.StartNew();

        var completePercent = (decimal)(_numMerges / 100);
        var bytesPairFrequeces = GetMaxBytePair();
        for (var i = 0; i < _numMerges && _vocab.Count() <= _vocabSize; i++)
        {
            var maxBytePair = GetMaxBytePair();
            _merges.Add(maxBytePair);

            var currentIndex = _vocab.Max(e => e.Key);
            var nextIndex = currentIndex += 1;
            var combinedBytePair = new byte[maxBytePair.Item1.Length + maxBytePair.Item2.Length];
            Buffer.BlockCopy(maxBytePair.Item1, 0, combinedBytePair, 0, maxBytePair.Item1.Length);
            Buffer.BlockCopy(maxBytePair.Item2, 0, combinedBytePair, maxBytePair.Item1.Length, maxBytePair.Item2.Length);

            _vocab.Add(nextIndex, combinedBytePair);
            _reversedVocab.Add(combinedBytePair, nextIndex);

            _strVocab.Add(nextIndex, Encoding.UTF8.GetString(combinedBytePair));

            var newPreTokenizedDict = new Dictionary<List<byte[]>, int>(_listByteArraySequenceComparer);
            foreach (var preToken in _preTokens)
            {
                // Список для построения нового слова после слияний
                var newPreToken = new List<byte[]>();

                //TODO Для каждой пары байтов хранить список пре-токенов в которых она есть

                // Проходим по всем токенам слова
                for (var e = 0; e < preToken.Key.Count; e++)
                {
                    // Проверяем, является ли текущая пара искомой:
                    // 1. i < preToken.Key.Count - 1 - есть следующий токен
                    // 2. preToken.Key[i] == pair.Item1 - текущий токен равен первому элементу пары
                    // 3. preToken.Key[i + 1] == pair.Item2 - следующий токен равен второму элементу
                    if (e < preToken.Key.Count - 1 &&
                        _byteArraySequenceEqualityComparer.Equals(preToken.Key[e], maxBytePair.Item1) &&
                        _byteArraySequenceEqualityComparer.Equals(preToken.Key[e + 1], maxBytePair.Item2))
                    {
                        newPreToken.Add(combinedBytePair);

                        e++;
                    }
                    else
                    {
                        newPreToken.Add(preToken.Key[e]);
                    }
                }

                newPreTokenizedDict.Add(newPreToken, preToken.Value);
            }

            // Рассчет прогресса
            var percentage = (float)(i + 1) / _numMerges * 100;
            var elapsed = stopwatch.Elapsed;

            // Оценка оставшегося времени
            var itemsPerSecond = (i + 1) / elapsed.TotalSeconds;
            var remaining = itemsPerSecond > 0
                ? TimeSpan.FromSeconds((_numMerges - (i + 1)) / itemsPerSecond)
                : TimeSpan.Zero;

            Console.Write($"\r{percentage:F1}% | {i + 1}/{_numMerges} | " +
                          $"Прошло: {elapsed:hh\\:mm\\:ss} | Осталось: {remaining:hh\\:mm\\:ss}");

            _preTokens = null;
            _preTokens = newPreTokenizedDict;
        }

        stopwatch.Stop();
        Console.Read();
    }*/

    /*private (byte[], byte[]) GetMaxBytePair()
    {
        var bytePairsFrequences = new Dictionary<(byte[], byte[]), int>(_byteArrayTupleEqualityComparer);
        foreach (var preToken in _preTokens)
        {
            for (var e = 1; e < preToken.Key.Count(); e++)
            {
                if (preToken.Key[e].Count() > 1)
                {
                }
                var bytePair = (preToken.Key[e - 1], preToken.Key[e]);

                if (bytePairsFrequences.ContainsKey(bytePair))
                {
                    bytePairsFrequences[bytePair] += preToken.Value;
                }
                else
                {
                    bytePairsFrequences[bytePair] = preToken.Value;
                }
            }
        }

        if (bytePairsFrequences == null)
            throw new ArgumentNullException(nameof(bytePairsFrequences));

        if (bytePairsFrequences.Count == 0)
            throw new InvalidOperationException("Dictionary is empty");

        var frequecedNum = bytePairsFrequences
            .Max(kvp => kvp.Value);

        var frequencedPairs = bytePairsFrequences.Where(e => e.Value == frequecedNum);

        return frequencedPairs.Select(e => e.Key).Max(_byteArrayTupleComparer);
    }*/


    //key = bytePair; value = List<preToken>


    // [[1,2],[2,3],[3,4],[4,5],[5,6]]

    //Нужно пройтись по пре токенам содержащим самую частую пару байт и слить в этих претокенах эту пару

    private void Pretokenize(string text)
    {
        //TODO список пар сделать на уровне пре-токенизации

        var matches = _pattern.Matches(text);
        foreach (Match preToken in matches)
        {
            var preTokenValue = preToken.Value;
            var preTokenBytes = Encoding.UTF8.GetBytes(preTokenValue);

            var preTokenBytesList = new List<byte[]>();

            for (var i = 0; i < preTokenBytes.Count(); i++)
            {
                var byteArr = new byte[1] { preTokenBytes[i] };
                preTokenBytesList.Add(byteArr);
            }

            if (_preTokens.ContainsKey(preTokenBytesList))
            {
                _preTokens[preTokenBytesList]++;
                _strPreTokens[preTokenValue]++;

                // Вычисляем общий размер
                int totalSize = preTokenBytesList.Sum(a => a.Length);
                byte[] result = new byte[totalSize];

                // Копируем каждый массив в результат
                int offset = 0;
                foreach (byte[] array in preTokenBytesList)
                {
                    Buffer.BlockCopy(array, 0, result, offset, array.Length);
                    offset += array.Length;
                }
            }
            else
            {
                // Вычисляем общий размер
                int totalSize = preTokenBytesList.Sum(a => a.Length);
                byte[] result = new byte[totalSize];

                // Копируем каждый массив в результат
                int offset = 0;
                foreach (byte[] array in preTokenBytesList)
                {
                    Buffer.BlockCopy(array, 0, result, offset, array.Length);
                    offset += array.Length;
                }
                _strPreTokens[preTokenValue] = 1;
                _preTokens[preTokenBytesList] = 1;
            }
        }
    }

    private void InitVocabularity(string[] specialTokens = null)
    {
        _vocab.Clear();
        _reversedVocab.Clear();
        _strVocab.Clear();

        int id = 0;

        // Добавляем специальные токены
        if (specialTokens != null)
        {
            foreach (var token in specialTokens)
            {
                var bytes = Encoding.UTF8.GetBytes(token);
                var key = Encoding.UTF8.GetString(bytes);

                _vocab[id] = bytes;
                _reversedVocab[bytes] = id;
                _strVocab[id] = key;
                id++;
            }
        }

        // Добавляем все байты (0-255)
        for (int i = 0; i < 256; i++)
        {
            var bytes = new byte[] { (byte)i };
            var key = Encoding.UTF8.GetString(bytes);

            _vocab[id] = bytes;
            _reversedVocab[bytes] = id;
            _strVocab[id] = key;
            id++;
        }
    }

    private async Task<ConcurrentDictionary<int, string>> ReadTXTDatasetAsync(
    string filePath,
    string tokenSeparator,
    int maxConcurrent = 4)
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
                try
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

                        var chunk = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);

                        chunkData[chunkIndex] = chunk;

                        if (chunkIndex % 10 == 0 || chunkIndex == actualNumChunks - 1)
                        {
                            Console.WriteLine($"Чанк {chunkIndex}: {start}-{end} байт, размер: {chunkLength} байт, прочитано: {totalBytesRead} байт");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при чтении чанка {chunkIndex}: {ex.Message}");
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        Console.WriteLine($"\nЧтение завершено за {stopwatch.Elapsed:mm\\:ss\\.ff}");
        Console.WriteLine($"Прочитано чанков: {chunkData.Count}");

        stopwatch.Stop();

        return chunkData;
    }
}