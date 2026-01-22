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
    private ConcurrentDictionary<Token, int> _tokenCount;

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

        Console.WriteLine($"Запуск обработки чанков");

        /*foreach (var chunk in chunks)
        {
            
        }*/
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
        var localTokensHashSet = new Dictionary<(Token, Token), HashSet<PreToken>>(chunkBlocks.Length * 10, new TokenPairComparer());

        //Проходимся по каждому блоку текста и претокенизируем его
        for (var i = 0; i < chunkBlocks.Length; i++)
        {
            //Проходимся по совпадениям и записиываем претокены в локальный словарь
            var matches = _pattern.Matches(chunkBlocks[i]);
            for (var j = 0; j < matches.Count; j++)
            {
                var preToken = new PreToken(matches[j].Value);
                if (localPreTokens.TryGetValue(preToken, out int currentCount))
                {
                    localPreTokens[preToken] = currentCount + 1;
                }
                else
                {
                    localPreTokens.Add(preToken, 1);
                }

                if (preToken.Tokens.Count == 1)
                {
                    continue;
                }

                //делаем маппинг пар байтов на претокены
                for (var k = 1; k < preToken.Tokens.Count; k++)
                {
                    if (localTokensHashSet.TryGetValue((preToken.Tokens[k - 1], preToken.Tokens[k]), out var tokenPreTokens))
                    {
                        localTokensHashSet[(preToken.Tokens[k - 1], preToken.Tokens[k])].Add(preToken);
                    }
                    else
                    {
                        localTokensHashSet.Add((preToken.Tokens[k - 1], preToken.Tokens[k]), new HashSet<PreToken> { preToken });
                    }
                }
            }
        }

        //мержим в основной словарь претокенов
        BulkInsertPreTokens(localPreTokens);
        BulkInsertTokensHashSet(localTokensHashSet);
    }

    private void BulkInsertPreTokens(Dictionary<PreToken, int> localPreTokens)
    {
        foreach (var kvp in localPreTokens)
        {
            _preTokens.AddOrUpdate(kvp.Key, kvp.Value, (key, old) => old + kvp.Value);
        }
    }

    private void BulkInsertTokensHashSet(Dictionary<(Token, Token), HashSet<PreToken>> localTokensHashSet)
    {
        foreach (var kvp in localTokensHashSet)
        {
            _tokensHashSet.AddOrUpdate(
                kvp.Key,
                k => kvp.Value,
                (k, existingSet) =>
                {
                    existingSet.UnionWith(kvp.Value);
                    return existingSet;
                }
            );
        }
    }

    //key = bytePair; value = List<preToken>

    // [[1,2],[2,3],[3,4],[4,5],[5,6]]

    //Нужно пройтись по пре токенам содержащим самую частую пару байт и слить в этих претокенах эту пару

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