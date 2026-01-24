namespace NoNPL;

public interface IBPETokenizer
{
    Task Train(string filePath, string tokenSeparator, int vocabSize, int numMerges, int maxConcurrent = 4);
}
