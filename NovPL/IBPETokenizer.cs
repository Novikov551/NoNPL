namespace NoNPL;

public interface IBPETokenizer
{
    Task Train(string filePath, string tokenSeparator, int maxConcurrent = 4, int chunkSize = 4096);
}
