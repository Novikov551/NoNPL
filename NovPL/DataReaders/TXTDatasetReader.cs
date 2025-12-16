using System.Reflection;
using System.Text;
namespace NoNPL.DataReaders;

public class TXTDatasetReader : IDisposable//TODO
{
    private readonly StreamReader _reader;
    private bool _disposed;
    private const string Separator = "<|endoftext|>";
    private readonly char[] _separatorChars = Separator.ToCharArray();

    public TXTDatasetReader(string resourceName, Assembly assembly = null)
    {
        if (string.IsNullOrEmpty(resourceName))
            throw new ArgumentNullException(nameof(resourceName));

        assembly ??= Assembly.GetCallingAssembly();

        var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found");

        _reader = new StreamReader(resourceStream, Encoding.UTF8, true, 4096);
    }

    public IEnumerable<string> ReadChunks()
    {
        var buffer = new char[4096];
        var chunkBuilder = new StringBuilder();
        var separatorBuffer = new StringBuilder();
        int separatorIndex = 0;
        int charsRead;

        while ((charsRead = _reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < charsRead; i++)
            {
                char currentChar = buffer[i];

                // Проверяем, соответствует ли текущий символ разделителю
                if (currentChar == _separatorChars[separatorIndex])
                {
                    separatorBuffer.Append(currentChar);
                    separatorIndex++;

                    // Если нашли полный разделитель
                    if (separatorIndex == _separatorChars.Length)
                    {
                        string chunk = chunkBuilder.ToString().Trim();
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            yield return chunk;
                        }

                        chunkBuilder.Clear();
                        separatorBuffer.Clear();
                        separatorIndex = 0;
                    }
                }
                else
                {
                    // Если частично совпавший разделитель оказался не полным
                    if (separatorIndex > 0)
                    {
                        chunkBuilder.Append(separatorBuffer.ToString());
                        separatorBuffer.Clear();
                        separatorIndex = 0;
                    }

                    chunkBuilder.Append(currentChar);
                }
            }
        }

        // Добавляем оставшиеся символы из separatorBuffer
        if (separatorIndex > 0)
        {
            chunkBuilder.Append(separatorBuffer.ToString());
        }

        // Возвращаем последний чанк
        string lastChunk = chunkBuilder.ToString().Trim();
        if (!string.IsNullOrEmpty(lastChunk))
        {
            yield return lastChunk;
        }
    }

    public async IAsyncEnumerable<string> ReadChunksAsync()
    {
        var buffer = new char[4096];
        var chunkBuilder = new StringBuilder();
        var separatorBuffer = new StringBuilder();
        int separatorIndex = 0;
        int charsRead;

        while ((charsRead = await _reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < charsRead; i++)
            {
                char currentChar = buffer[i];

                if (currentChar == _separatorChars[separatorIndex])
                {
                    separatorBuffer.Append(currentChar);
                    separatorIndex++;

                    if (separatorIndex == _separatorChars.Length)
                    {
                        string chunk = chunkBuilder.ToString().Trim();
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            yield return chunk;
                        }

                        chunkBuilder.Clear();
                        separatorBuffer.Clear();
                        separatorIndex = 0;
                    }
                }
                else
                {
                    if (separatorIndex > 0)
                    {
                        chunkBuilder.Append(separatorBuffer.ToString());
                        separatorBuffer.Clear();
                        separatorIndex = 0;
                    }

                    chunkBuilder.Append(currentChar);
                }
            }
        }

        if (separatorIndex > 0)
        {
            chunkBuilder.Append(separatorBuffer.ToString());
        }

        string lastChunk = chunkBuilder.ToString().Trim();
        if (!string.IsNullOrEmpty(lastChunk))
        {
            yield return lastChunk;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _reader?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}