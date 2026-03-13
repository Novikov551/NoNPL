using BenchmarkDotNet.Attributes;
using NoNPL.DataReaders;
using NoNPL.Entities;
using NoNPL.Extensions;
using NoNPL.Services.Serializers;
using NoNPL.Services.Storage;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NoNPL;

[MemoryDiagnoser]
[RankColumn]
public class BPETokenizer
{
    private List<TokenPair> _merges = [];

    private ConcurrentDictionary<Token, int> _vocab;
    private ConcurrentDictionary<PreToken, int> _preTokens;
    private ConcurrentDictionary<TokenPair, HashSet<PreToken>> _tokenPairsHashSet;
    private ConcurrentDictionary<TokenPair, int> _tokenPairsCount;

    private readonly static Regex _pattern = new Regex(@"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.Compiled);
    private readonly TXTDatasetReader _tXTDatasetReader;
    private readonly VocabStorage _vocabStorage;

    public BPETokenizer(string resultsFolderLocation,
        VocabFileFormat fileFormat)
    {
        _vocab = new();
        _merges = new();
        _preTokens = new();

        _tokenPairsHashSet = new();
        _tokenPairsCount = new();
        _tXTDatasetReader = new();

        _vocabStorage = new VocabStorage(resultsFolderLocation, SerializerFactory.Create(fileFormat));
    }

    public async Task LoadVocabAsync(CancellationToken ct = default)
    {
        var (vocab, merges, latestVersion) = await _vocabStorage.LoadLatestAsync(ct);

        _vocab = vocab;
        _merges = merges;
    }

    public async Task SaveVocabAsync(CancellationToken ct = default)
    {
        await _vocabStorage.SaveNextAsync(_vocab, _merges, ct);
        AdvancedConsole.WriteLine($"Vocab saved!", ConsoleMessageType.Success);
    }

    public async Task TrainAsync(string filePath,
        string tokenSeparator,
        int vocabSize,
        int maxConcurrent = 32,
        CancellationToken ct = default)
    {
        var generalStopwatch = Stopwatch.StartNew();

        InitVocabularity();
        var splitToken = UTF8Converter.GetBytes(tokenSeparator);

        await foreach (var textBlock in _tXTDatasetReader.ReadTXTDatasetAsync(filePath: filePath,
            splitToken: splitToken,
            bufferSize: 700 * 1024 * 1024,
            cancellationToken: ct))
        {
            var chunks = SplitTextBlockByToken(textBlock, splitToken, maxConcurrent);

            ProcessChunks(chunks, splitToken, maxConcurrent);
        }

        var stopwatch = Stopwatch.StartNew();
        AdvancedConsole.WriteLine($"Start merging pairs of bytes...");

        var counter = _vocab.Count;
        var result = true;
        while (result && _vocab.Count <= vocabSize)
        {
            result = MergePairs();
            if (counter % 10 == 0)
            {
                AdvancedConsole.WriteProgress(_vocab.Count, vocabSize, "Progress:", 50, ConsoleMessageType.Warning);
            }
            counter++;
        }

        AdvancedConsole.WriteLine($"Completing the merging of byte pairs... Duration: {stopwatch.Elapsed:mm\\:ss\\.ff}\n", ConsoleMessageType.Success);

        AdvancedConsole.WriteLine($"Dataset processed. Duration: {generalStopwatch.Elapsed:mm\\:ss\\.ff}\n", ConsoleMessageType.Success);

        generalStopwatch.Stop();
    }

    public static List<byte[]> SplitTextBlockByToken(byte[] fragment, byte[] tokenBytes, int maxConcurrent)
    {
        if (fragment == null) throw new ArgumentNullException(nameof(fragment));
        if (tokenBytes == null || tokenBytes.Length == 0)
            throw new ArgumentException("Token cannot be null or empty.", nameof(tokenBytes));
        if (maxConcurrent <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "maxConcurrent must be positive.");

        // 1. Найти все позиции сразу после токена (концы записей)
        var endPositions = new List<int>();
        var dataSpan = fragment.AsSpan();
        var tokenSpan = tokenBytes.AsSpan();
        int pos = 0;
        while (pos <= dataSpan.Length - tokenSpan.Length)
        {
            int idx = dataSpan.Slice(pos).IndexOf(tokenSpan);
            if (idx < 0) break;
            int absIdx = pos + idx;
            endPositions.Add(absIdx + tokenSpan.Length);
            pos = absIdx + tokenSpan.Length;
        }

        // Если токен не найден, возвращаем весь фрагмент как одну часть
        if (endPositions.Count == 0)
        {
            return new List<byte[]> { fragment };
        }

        // Если последняя запись не доходит до конца фрагмента (хвост без токена), добавляем искусственную границу
        if (endPositions.Last() < fragment.Length)
        {
            endPositions.Add(fragment.Length);
        }

        int recordCount = endPositions.Count; // количество записей (каждая заканчивается токеном или концом)
        int desiredGroups = Math.Min(maxConcurrent, recordCount);

        // Если желаемое количество групп равно количеству записей, возвращаем каждую запись отдельно
        if (desiredGroups == recordCount)
        {
            var result = new List<byte[]>(recordCount);
            int prev = 0;
            foreach (int end in endPositions)
            {
                byte[] part = new byte[end - prev];
                Array.Copy(fragment, prev, part, 0, part.Length);
                result.Add(part);
                prev = end;
            }
            return result;
        }

        // 2. Вычислить размеры записей и кумулятивные суммы
        long[] cumulativeSizes = new long[recordCount];
        int start = 0;
        for (int i = 0; i < recordCount; i++)
        {
            int size = endPositions[i] - start;
            cumulativeSizes[i] = (i == 0) ? size : cumulativeSizes[i - 1] + size;
            start = endPositions[i];
        }

        long totalSize = fragment.Length;
        double targetChunkSize = (double)totalSize / desiredGroups;

        // 3. Определить индексы записей, на которых будут заканчиваться группы (кроме последней)
        List<int> groupEndIndices = new List<int>();
        for (int g = 1; g < desiredGroups; g++)
        {
            double targetCum = g * targetChunkSize;
            int idx = FindClosestIndex(cumulativeSizes, targetCum);
            groupEndIndices.Add(idx);
        }

        // 4. Сформировать части, используя накопленные границы
        var parts = new List<byte[]>(desiredGroups);
        int prevEndByte = 0;
        foreach (int idx in groupEndIndices)
        {
            int currentEndByte = endPositions[idx];
            byte[] part = new byte[currentEndByte - prevEndByte];
            Array.Copy(fragment, prevEndByte, part, 0, part.Length);
            parts.Add(part);
            prevEndByte = currentEndByte;
        }

        // Последняя часть – от prevEndByte до конца фрагмента
        if (prevEndByte < fragment.Length)
        {
            byte[] lastPart = new byte[fragment.Length - prevEndByte];
            Array.Copy(fragment, prevEndByte, lastPart, 0, lastPart.Length);
            parts.Add(lastPart);
        }

        return parts;
    }

    // Вспомогательный метод для бинарного поиска ближайшего индекса
    private static int FindClosestIndex(long[] cumulative, double target)
    {
        int lo = 0;
        int hi = cumulative.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (cumulative[mid] == target)
                return mid;
            if (cumulative[mid] < target)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        if (lo >= cumulative.Length) return hi;
        if (hi < 0) return lo;

        double diffLo = cumulative[lo] - target;
        double diffHi = target - cumulative[hi];
        return diffLo < diffHi ? lo : hi;
    }

    public async Task<List<int>> EncodeAsync(string text)
    {
        var stopwatch = Stopwatch.StartNew();

        AdvancedConsole.WriteLine($"Start of tokenization...");

        var tokens = new List<int>(text.Length);
        foreach (ValueMatch match in _pattern.EnumerateMatches(text))
        {
            var matchValue = text.AsSpan(match.Index, match.Length);
            var preToken = new PreToken(matchValue.ToString());

            if (preToken.Tokens.IsNullOrEmpty())
            {
                continue;
            }

            tokens.AddRange(TokenizePreToken(preToken.Tokens.Values.ToList()));
        }

        AdvancedConsole.WriteLine($"Tokenization is complete. Duration: {stopwatch.Elapsed:mm\\:ss\\.ff}\n", ConsoleMessageType.Success);

        stopwatch.Stop();

        return tokens;
    }

    public string Decode(IEnumerable<int> tokenIds)
    {
        if (tokenIds == null)
            return string.Empty;

        var allBytes = new List<byte>();

        foreach (int tokenId in tokenIds)
        {
            var token = GetToken(tokenId);
            if (token != null)
            {
                allBytes.AddRange(token.Bytes);
            }
        }

        var encoding = (Encoding)Encoding.UTF8.Clone();
        encoding.DecoderFallback = new DecoderReplacementFallback("\uFFFF");

        return encoding.GetString(allBytes.ToArray());
    }

    public Token GetToken(int value)
    {
        foreach (var kvp in _vocab)
        {
            if (kvp.Value == value)
            {
                return kvp.Key;
            }
        }

        return null;
    }

    private List<int> TokenizePreToken(List<Token> tokens)
    {
        var currentTokens = new List<Token>(tokens);
        bool pairFound;

        do
        {
            pairFound = false;

            for (int i = 0; i < currentTokens.Count - 1; i++)
            {
                var pairToken = new Token(new TokenPair(currentTokens[i], currentTokens[i + 1]));

                if (_vocab.TryGetValue(pairToken, out var _))
                {
                    var newTokens = new List<Token>();

                    for (int j = 0; j < i; j++)
                    {
                        newTokens.Add(currentTokens[j]);
                    }

                    newTokens.Add(pairToken);

                    for (int j = i + 2; j < currentTokens.Count; j++)
                    {
                        newTokens.Add(currentTokens[j]);
                    }

                    currentTokens = newTokens;
                    pairFound = true;
                    break;
                }
            }
        } while (pairFound);

        var result = new List<int>();
        foreach (var token in currentTokens)
        {
            result.Add(_vocab[token]);
        }

        return result;
    }

    private TokenPair? GetFrequensedTokensPair()
    {
        if (_tokenPairsCount.IsEmpty)
            return null;

        TokenPair bestPair = default;
        int maxCount = 0;
        bool isFirst = true;

        foreach (var kvp in _tokenPairsCount)
        {
            if (isFirst)
            {
                bestPair = kvp.Key;
                maxCount = kvp.Value;
                isFirst = false;
                continue;
            }

            if (kvp.Value > maxCount)
            {
                maxCount = kvp.Value;
                bestPair = kvp.Key;
            }
            else if (kvp.Value == maxCount)
            {
                if (CompareTokenPairs(kvp.Key, bestPair) > 0)
                {
                    bestPair = kvp.Key;
                }
            }
        }

        return bestPair;
    }

    private int CompareTokenPairs(TokenPair pair1, TokenPair pair2)
    {
        int firstComparison = CompareTokens(pair1.First, pair2.First);
        if (firstComparison != 0)
            return firstComparison;

        return CompareTokens(pair1.Second, pair2.Second);
    }

    private int CompareTokens(Token token1, Token token2)
    {
        if (ReferenceEquals(token1, token2))
            return 0;

        if (token1 is null)
            return -1;

        if (token2 is null)
            return 1;

        byte[] bytes1 = token1.Bytes;
        byte[] bytes2 = token2.Bytes;

        int minLength = Math.Min(bytes1.Length, bytes2.Length);
        for (int i = 0; i < minLength; i++)
        {
            int comparison = bytes1[i].CompareTo(bytes2[i]);
            if (comparison != 0)
                return comparison;
        }

        return bytes1.Length.CompareTo(bytes2.Length);
    }

    private bool MergePairs()
    {
        if (_tokenPairsCount.Count == 0)
        {
            return false;
        }

        var frequensedPair = GetFrequensedTokensPair();
        if (!frequensedPair.HasValue)
        {
            return false;
        }

        var needMergePretokens = _tokenPairsHashSet[frequensedPair.Value];
        foreach (var needMergePreToken in needMergePretokens)
        {
            var preTokenCount = _preTokens[needMergePreToken];

            var tokenPairs = needMergePreToken.MergePair(frequensedPair.Value);

            foreach (var needUpdatePair in tokenPairs.NeedUpdateCounterPairs)
            {
                if (_tokenPairsHashSet.TryGetValue(needUpdatePair, out var _))
                {
                    _tokenPairsHashSet[needUpdatePair].Remove(needMergePreToken);
                    _tokenPairsCount[needUpdatePair] -= preTokenCount;
                    if (_tokenPairsCount[needUpdatePair] == 0)
                    {
                        _tokenPairsCount.Remove(needUpdatePair, out var _);
                        _tokenPairsHashSet.Remove(needUpdatePair, out var _);
                    }
                }
            }

            foreach (var newPair in tokenPairs.NewPairs)
            {
                _tokenPairsHashSet.AddOrUpdate(
                   newPair,
                   k => new HashSet<PreToken>() { needMergePreToken },
                   (k, existingSet) =>
                   {
                       existingSet.Add(needMergePreToken);
                       return existingSet;
                   }
               );

                _tokenPairsCount.AddOrUpdate(newPair, preTokenCount, (k, v) => v + preTokenCount);
            }
        }

        if (!_tokenPairsHashSet.Remove(frequensedPair.Value, out var _))
        {
            throw new Exception();
        }

        if (!_tokenPairsCount.Remove(frequensedPair.Value, out var _))
        {
            throw new Exception();
        }

        _merges.Add(frequensedPair.Value);

        var vocabToken = new Token(frequensedPair.Value);
        _vocab.TryAdd(vocabToken, _vocab.Max(e => e.Value) + 1);

        return true;
    }

    private void ProcessChunks(List<byte[]> chunks,
        byte[] tokenSeparator,
        int maxConcurrent = 32)
    {
        var partitioner = Partitioner.Create(chunks, EnumerablePartitionerOptions.NoBuffering);
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrent
        };

        var lockObj = new object();
        var totalChunks = chunks.Count();
        var counter = 0;

        Parallel.ForEach(partitioner, options, chunk =>
        {
            ProcessChunk(chunk, tokenSeparator);
            counter++;
            lock (lockObj)
            {
                AdvancedConsole.WriteProgress(counter, totalChunks, "Progress:", 60, ConsoleMessageType.Error);
            }
        });
    }

    private List<ReadOnlyMemory<byte>> GetChunkBlocks(ReadOnlyMemory<byte> chunk, byte[] tokenSeparator)
    {
        var span = chunk.Span;
        var separatorSpan = tokenSeparator.AsSpan();
        var result = new List<ReadOnlyMemory<byte>>();

        int start = 0;
        while (true)
        {
            int end = span.Slice(start).IndexOf(separatorSpan);
            if (end == -1)
            {
                // Последний блок (до конца)
                if (start < span.Length)
                {
                    result.Add(chunk.Slice(start));
                }
                break;
            }

            end += start; // абсолютная позиция начала разделителя

            // Добавляем блок перед разделителем, если он не пустой
            if (end > start)
            {
                result.Add(chunk.Slice(start, end - start));
            }

            start = end + tokenSeparator.Length;
        }

        return result;
    }

    private void ProcessChunk(byte[] chunk, byte[] tokenSeparator)
    {
        var chunkBlocks = GetChunkBlocks(chunk, tokenSeparator); 
        var capacity = chunk.Length / 5;
        var preTokens = new Dictionary<PreToken, int>(capacity);
        var tokenPairData = new Dictionary<TokenPair, PairData>(capacity * 2);

        foreach (var block in chunkBlocks)
        {
            var decoded = UTF8Converter.DecodeToSpan(block.Span);
            foreach (var match in _pattern.EnumerateMatches(decoded.Span))
            {
                var matchValue = decoded.Span.Slice(match.Index, match.Length);
                var preToken = new PreToken(matchValue);

                if (preToken.Tokens.IsNullOrEmpty())
                    continue;

                ref int preTokenCount = ref CollectionsMarshal.GetValueRefOrAddDefault(preTokens, preToken, out _);
                preTokenCount++;

                if (preToken.Tokens.Count <= 1)
                    continue;

                ProcessTokenPairs(preToken, preTokenCount, tokenPairData);
            }
        }

        BulkInsertPreTokens(preTokens);
        BulkInsertTokenPairData(tokenPairData);
    }

    private void ProcessTokenPairs(
        PreToken preToken,
        int preTokenCount,
        Dictionary<TokenPair, PairData> tokenPairData)
    {
        foreach(var token in preToken.Tokens)
        {
            if(preToken.Tokens.TryGetValue(token.Key + 1, out var secondToken))
            {
                var tokenPair = new TokenPair(token.Value, secondToken);

                ref PairData pairData = ref CollectionsMarshal.GetValueRefOrAddDefault(tokenPairData, tokenPair, out bool exists);
                if (!exists)
                {
                    pairData = new PairData(preToken) { Count = preTokenCount };
                }
                else
                {
                    pairData.PreTokens.Add(preToken);
                    pairData.Count++;
                }
            }
        }
    }

    private void BulkInsertPreTokens(Dictionary<PreToken, int> localPreTokens)
    {
        foreach (var kvp in localPreTokens)
        {
            _preTokens.AddOrUpdate(kvp.Key, kvp.Value, (key, old) => old + kvp.Value);
        }
    }

    private void BulkInsertTokenPairData(Dictionary<TokenPair, PairData> tokenPairData)
    {
        foreach (var kvp in tokenPairData)
        {
            _tokenPairsHashSet[kvp.Key] = kvp.Value.PreTokens;
            _tokenPairsCount[kvp.Key] = kvp.Value.Count;
        }
    }

    private void InitVocabularity()
    {
        _vocab.Clear();

        int id = 0;

        // Добавляем все байты (0-255)
        for (int i = 0; i < 256; i++)
        {
            var bytes = new byte[] { (byte)i };
            var token = new Token(bytes);

            _vocab[token] = id;
            id++;
        }
    }
}