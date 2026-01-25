using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace NoNPL.Benchmark
{
    [MemoryDiagnoser]
    [RankColumn]
    public class BPETokenizerBenchmarks
    {
        private BPETokenizer _tokenizer;

        [GlobalSetup]
        public void Setup()
        {
            // Инициализация токенизатора с паттерном для слов и символов
            _tokenizer = new BPETokenizer(@"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+");
        }

        [Benchmark]
        public async Task TrainSmallDataset()
        {
            try
            {
                await _tokenizer.Train(
                    "C:\\Users\\nikit\\OneDrive\\Desktop\\raznoe\\Projects\\CS336\\NovPL\\NovPL\\Datasets\\TinyStories-test.txt",
                    "<|endoftext|>",
                    404,
                    12);
            }
            catch(Exception ex)
            {

            }
        }

        [Benchmark]
        public async Task TrainMediumDataset()
        {
            try
            {
                await _tokenizer.Train(
                    "C:\\Users\\nikit\\OneDrive\\Desktop\\raznoe\\Projects\\CS336\\NovPL\\NovPL\\Datasets\\TinyStories-valid.txt",
                    "<|endoftext|>",
                    1000,
                    12);
            }
            catch (Exception ex)
            {

            }
        }

        /*[Benchmark]
        public async Task TrainLargeDataset()
        {
            try
            {
                await _tokenizer.Train(
                    "C:\\Users\\nikit\\OneDrive\\Desktop\\raznoe\\Projects\\CS336\\NovPL\\NovPL\\Datasets\\TinyStories-train.txt",
                    "<|endoftext|>",
                    10000,
                    12);
            }
            catch (Exception ex)
            {

            }
        }*/

        [Benchmark]
        public void GetFrequensedTokensPair_Benchmark()
        {
            // Тестируем отдельно метод поиска частых пар
            for (int i = 0; i < 100; i++)
            {
                _tokenizer.GetFrequensedTokensPair();
            }
        }

        [Benchmark]
        public void ProcessChunkBenchmark()
        {
            // Тестируем обработку одного чанка
            var chunk = new KeyValuePair<int, string>(0, GenerateTestText(100000));

            // Используем рефлексию для вызова приватного метода
            var method = typeof(BPETokenizer).GetMethod("ProcessChunk",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method?.Invoke(_tokenizer, [chunk, "<|endoftext|>"]);
        }

        private string GenerateTestText(int length)
        {
            var words = new[] { "<|endoftext|>", "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog", "hello", "world", "test", "data" };
            var random = new Random(42); // Фиксированный seed для воспроизводимости

            var result = new List<string>();
            for (int i = 0; i < length; i++)
            {
                result.Add(words[random.Next(words.Length)]);
            }

            return string.Join(" ", result);
        }
    }
}
