using NoNPL;
using NoNPL.Extensions;
using NoNPL.Services.Serializers;
Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0)
{
    AdvancedConsole.WriteLine("Specify the path to dataset.", ConsoleMessageType.Error);
    return;
}
else
{
    if (args.Count() < 3)
    {
        AdvancedConsole.WriteLine($"Введены не все параметры.", ConsoleMessageType.Error);
    }

    int vocabSize;
    if (int.TryParse(args[0], out int parsedVocabSize))
    {
        vocabSize = parsedVocabSize;
    }
    else
    {
        AdvancedConsole.WriteLine($"Некорректное значение при указании размера словаря `{args[0]}`.Ведите число.", ConsoleMessageType.Error);
        return;
    }

    VocabFileFormat fileFormat;
    if (Enum.TryParse(args[1], true, out VocabFileFormat parsedFormat))
    {
        fileFormat = parsedFormat;
    }
    else
    {
        AdvancedConsole.WriteLine($"Некорректное значение при указании формата для сохранения словаря `{args[1]}`." +
            $"Ведите один из следующих вариантов:{string.Join(", ", Enum.GetNames<VocabFileFormat>())}", ConsoleMessageType.Error);
        return;
    }

    var inputFilePath = args[2];
    if (inputFilePath.IsNullOrEmpty())
    {
        AdvancedConsole.WriteLine($"Некорректный пусть к датасету. Пожалуйсте, введите полный путь до файла.", ConsoleMessageType.Error);
        return;
    }

    var pattern = @"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+";

    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    var trainResultsPath = Path.Combine(baseDirectory, "TrainResults");

    var tokenizer = new BPETokenizer(pattern, trainResultsPath, fileFormat);

    AdvancedConsole.WriteLine($"Лог-процессоров:{ Environment.ProcessorCount}", ConsoleMessageType.Warning);

    await tokenizer.TrainAsync(inputFilePath,
        "<|endoftext|>",
        vocabSize,
        32);

    await tokenizer.SaveVocabAsync();
}