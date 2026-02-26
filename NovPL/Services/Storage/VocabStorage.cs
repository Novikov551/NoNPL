using NoNPL.Entities;
using NoNPL.Services.Serializers;
using System.Collections.Concurrent;

namespace NoNPL.Services.Storage
{
    public class VocabStorage
    {
        private readonly string _folderPath;
        private readonly ISerializer _serializer;
        private readonly string _fileExtension;

        public VocabStorage(string folderPath, ISerializer serializer)
        {
            _folderPath = folderPath;
            _serializer = serializer;
            _fileExtension = serializer switch
            {
                JsonSerializer => ".json",
                MessagePackSerializer => ".msgpack",
                _ => throw new NotSupportedException("Unsupported serializer type")
            };
            Directory.CreateDirectory(_folderPath);
        }

        private string GetFilePath(int version)
            => Path.Combine(_folderPath, $"vocab_data_v{version}{_fileExtension}");

        private IEnumerable<int> GetExistingVersions()
        {
            var pattern = $"vocab_data_v*{_fileExtension}";
            foreach (string file in Directory.GetFiles(_folderPath, pattern))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.StartsWith("vocab_data_v"))
                {
                    var versionPart = fileName.Substring("vocab_data_v".Length);
                    if (int.TryParse(versionPart, out int version))
                        yield return version;
                }
            }
        }

        public int GetNextVersion()
        {
            var versions = GetExistingVersions();
            return versions.Any() ? versions.Max() + 1 : 1;
        }

        public async Task SaveNextAsync(ConcurrentDictionary<Token, int> vocab, 
            List<TokenPair> merges, 
            CancellationToken ct = default)
        {
            var version = GetNextVersion();
            var path = GetFilePath(version);
            await _serializer.SaveAsync(path, vocab, merges, ct);
        }

        public async Task SaveAsync(int version, 
            ConcurrentDictionary<Token, int> vocab, 
            List<TokenPair> merges,
            CancellationToken ct = default)
        {
            var path = GetFilePath(version);
            await _serializer.SaveAsync(path, vocab, merges, ct);
        }

        public async Task<(ConcurrentDictionary<Token, int> Vocab, List<TokenPair> Merges, int Version)> 
            LoadLatestAsync(CancellationToken ct = default)
        {
            var versions = GetExistingVersions().ToList();
            if (!versions.Any())
                throw new FileNotFoundException($"No files found in folder '{_folderPath}' for format {_fileExtension}.");

            var latest = versions.Max();
            var path = GetFilePath(latest);
            var (vocab, merges) = await _serializer.LoadAsync(path, ct);
            return (vocab, merges, latest);
        }

        public async Task<(ConcurrentDictionary<Token, int> Vocab, List<TokenPair> Merges)> LoadAsync(int version)
        {
            var path = GetFilePath(version);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Version {version} not found (expected file: {path}).");
            return await _serializer.LoadAsync(path);
        }

        public IReadOnlyList<int> GetAllVersions() => GetExistingVersions().ToList();
    }
}
