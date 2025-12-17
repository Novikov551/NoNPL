using NoNPL.Comparers;
using NoNPL.DataReaders;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NoNPL;

public class BPETokenizer : IBPETokenizer
{
    private Dictionary<int, byte[]> _vocab;
    private Dictionary<byte[], int> _reversedVocab;
    private List<(byte[], byte[])> _merges = new List<(byte[], byte[])>();
    private Dictionary<List<byte[]>, int> _preTokens;
    private Dictionary<string, int> _strPreTokens;
    //Для дебага
    private Dictionary<int, string> _strVocab;

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

    public async Task Train(string filePath, string tokenSeparator, int maxConcurrent = 4, int chunkSize = 4096)
    {
        //Инициализируем словарь
        InitVocabularity([tokenSeparator]);

        var chunks = await ReadTXTDatasetAsync(filePath, tokenSeparator, maxConcurrent, chunkSize);
        //Претокенизируем текст
        /*    PreTokenize();*/

        //Делаем слияния
        MergePairs();
    }

    private async Task<ConcurrentDictionary<int, string>> ReadTXTDatasetAsync(string filePath, string tokenSeparator, int maxConcurrent = 4, int chunkSize = 4096)
    {
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);

        var chunkData = new ConcurrentDictionary<int, (string mainPart, string tail)>();
        var tasks = new List<Task>();

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

                        //Прочитали чанк
                        var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        // Сразу находим последнее полное вхождение разделителя
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
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        return await ConcatenateTailsAsync(chunkData);
    }

    private async Task<ConcurrentDictionary<int, string>> ConcatenateTailsAsync(
        ConcurrentDictionary<int, (string mainPart, string tail)> chunkData)
    {
        var finalChunks = new ConcurrentDictionary<int, string>();
        var sortedKeys = chunkData.Keys.OrderBy(k => k).ToArray();

        await Parallel.ForEachAsync(sortedKeys, async (chunkIndex, cancellationToken) =>
        {
            var current = chunkData[chunkIndex];

            // Если это первый чанк, берем только основную часть
            if (chunkIndex == 0)
            {
                finalChunks[chunkIndex] = current.mainPart;
                return;
            }

            // Для остальных чанков добавляем хвост предыдущего
            if (chunkData.TryGetValue(chunkIndex - 1, out var previous))
            {
                finalChunks[chunkIndex] = previous.tail + current.mainPart;
            }
            else
            {
                finalChunks[chunkIndex] = current.mainPart;
            }
        });

        return finalChunks;
    }

    private void MergePairs()
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
    }

    private (byte[], byte[]) GetMaxBytePair()
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
    }


    //key = bytePair; value = List<preToken>


    // [[1,2],[2,3],[3,4],[4,5],[5,6]]

    //Нужно пройтись по пре токенам содержащим самую частую пару байт и слить в этих претокенах эту пару


    private void PreTokenize(string chunk)
    {
        //TODO список пар сделать на уровне пре-токенизации

        var stopwatch = Stopwatch.StartNew();
        var processedChunks = 0;
        var lastUpdate = DateTime.Now;

        var matches = _pattern.Matches(chunk);
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
            }
            else
            {
                _preTokens[preTokenBytesList] = 1;
                _strPreTokens[preTokenValue] = 1;
            }
        }

        processedChunks++;

        if ((DateTime.Now - lastUpdate).TotalSeconds >= 1)
        {
            Console.Write($"\rОбработано чанков: {processedChunks} | " +
                             $"Время выполнения: {stopwatch.Elapsed:hh\\:mm\\:ss} | " +
                             $"Скорость: {processedChunks / stopwatch.Elapsed.TotalMinutes:F1} чанк/мин");
            lastUpdate = DateTime.Now;
        }

        stopwatch.Stop();
        Console.WriteLine($"Итого: обработано {processedChunks} чанков за {stopwatch.Elapsed:hh\\:mm\\:ss}");
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
}