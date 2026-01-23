using NoNPL.Comparers;
using NoNPL.DataReaders;
using NoNPL.Entities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NoNPL;

public class BPETokenizer : IBPETokenizer
{
    private ConcurrentDictionary<Token, int> _vocab;
    private List<(Token, Token)> _merges = [];
    private ConcurrentDictionary<PreToken, int> _preTokens;
    private ConcurrentDictionary<(Token, Token), HashSet<PreToken>> _tokensHashSet;
    private ConcurrentDictionary<(Token, Token), int> _tokenCount;

    private readonly Regex _pattern;

    private readonly int _vocabSize;
    private readonly int _numMerges;

    private readonly TXTDatasetReader _tXTDatasetReader;

    public BPETokenizer(string pattern, int vocabSize, int numMerges)
    {
        _vocabSize = vocabSize;
        _numMerges = numMerges;

        _pattern = new Regex(pattern, RegexOptions.Compiled);
        _vocab = new();
        _merges = new();
        _preTokens = new();
        _tokensHashSet = new(new TokenPairComparer());
        _tokenCount = new(new TokenPairComparer());
        _tXTDatasetReader = new();
    }

    public async Task Train(string filePath, string tokenSeparator, int maxConcurrent = 12)
    {
        InitVocabularity([tokenSeparator]);

        var chunks = await _tXTDatasetReader.ReadTXTDatasetAsync(filePath, tokenSeparator, maxConcurrent);

        ProcessChunks(chunks, tokenSeparator);

        /* MergePairs();*/
    }

    private void ProcessChunks(ConcurrentDictionary<int, string> chunks, string tokenSeparator, int maxConcurrent = 12)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrent
        };

        Console.WriteLine($"Запуск обработки чанков\n");

        var blocksCount = 0;

        foreach(var chunk in chunks )
        {
            blocksCount += chunk.Value.Split(tokenSeparator).Count();
        }

        Console.WriteLine($"Блоков:{blocksCount}\n");

        Parallel.ForEach(chunks, options, chunk =>
        {
            ProcessChunk(chunk, tokenSeparator);
        });

        Console.WriteLine($"Обработано за {stopwatch.Elapsed:mm\\:ss\\.ff}");

        stopwatch.Stop();
    }

    private void ProcessChunk(KeyValuePair<int, string> chunk, string tokenSeparator)
    {
        //Получаем блоки текста внутри чанка
        var chunkBlocks = chunk.Value.Split(tokenSeparator);

        //инициализируем словарь сразу с примерным размером для ускорения выполнения
        var localPreTokens = new Dictionary<PreToken, int>(chunkBlocks.Length * 10);
        var localTokensHashSet = new Dictionary<(Token, Token), HashSet<PreToken>>(chunkBlocks.Length * 50, new TokenPairComparer());
        var localTokenCount = new Dictionary<(Token, Token), int>(chunkBlocks.Length * 50, new TokenPairComparer());

        //Проходимся по каждому блоку текста и претокенизируем его
        for (var i = 0; i < chunkBlocks.Length; i++)
        {
            //Проходимся по совпадениям и записиываем претокены в локальный словарь
            var matches = _pattern.Matches(chunkBlocks[i]);
            for (var j = 0; j < matches.Count; j++)
            {
                var preToken = new PreToken(matches[j].Value);
                int preTokenCount;
                if (localPreTokens.TryGetValue(preToken, out int currentCount))
                {
                    preTokenCount = currentCount + 1;
                    localPreTokens[preToken] = preTokenCount;
                }
                else
                {
                    preTokenCount = 1;
                    localPreTokens.Add(preToken, preTokenCount);
                }

                if (preToken.Tokens.Count == 1)
                {
                    continue;
                }
                else
                {
                    //делаем маппинг пар байтов на претокены
                    for (var k = 1; k < preToken.Tokens.Count; k++)
                    {
                        var tokenPair = (preToken.Tokens[k - 1], preToken.Tokens[k]);

                        //добавляем в маппинг пар байтов на претокены
                        if (localTokensHashSet.TryGetValue(tokenPair, out var tokenPreTokens))
                        {
                            localTokensHashSet[tokenPair].Add(preToken);
                        }
                        else
                        {
                            localTokensHashSet.Add(tokenPair, new HashSet<PreToken> { preToken });
                        }

                        //добавляем в подсчет пар байтов
                        if (localTokenCount.TryGetValue(tokenPair, out int tokenCount))
                        {
                            localTokenCount[tokenPair] = tokenCount + preTokenCount;
                        }
                        else
                        {
                            localTokenCount.Add(tokenPair, preTokenCount);
                        }
                    }
                }
            }
        }

        //мержим в основные
        BulkInsertPreTokens(localPreTokens);
        BulkInsertTokensHashSet(localTokensHashSet, localTokenCount);
    }

    private void BulkInsertPreTokens(Dictionary<PreToken, int> localPreTokens)
    {
        foreach (var kvp in localPreTokens)
        {
            _preTokens.AddOrUpdate(kvp.Key, kvp.Value, (key, old) => old + kvp.Value);
        }
    }

    private void BulkInsertTokensHashSet(
        Dictionary<(Token, Token), HashSet<PreToken>> localTokensHashSet,
        Dictionary<(Token, Token), int> localTokenCount)
    {
        foreach (var kvp in localTokensHashSet)
        {
            _tokensHashSet.AddOrUpdate(
                kvp.Key,
                k => kvp.Value,
                (k, existingSet) =>
                {
                    lock (existingSet)
                    {
                        existingSet.UnionWith(kvp.Value);
                        return existingSet;
                    }
                }
            );

            var tokenPairCount = localTokenCount[kvp.Key];
            _tokenCount.AddOrUpdate(kvp.Key, tokenPairCount, (key, old) => old + tokenPairCount);
        }
    }

    private void InitVocabularity(string[] specialTokens = null)
    {
        _vocab.Clear();

        int id = 0;

        // Добавляем специальные токены
        if (specialTokens != null)
        {
            foreach (var specialToken in specialTokens)
            {
                var bytes = Encoding.UTF8.GetBytes(specialToken);
                var token = new Token(bytes);

                _vocab[token] = id;
                id++;
            }
        }

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