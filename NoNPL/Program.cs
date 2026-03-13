using NoNPL;
using NoNPL.Extensions;
using NoNPL.Services.Serializers;
Console.OutputEncoding = System.Text.Encoding.UTF8;

/*if (args.Length == 0)
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

    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    var trainResultsPath = Path.Combine(baseDirectory, "TrainResults");

    var tokenizer = new BPETokenizer(trainResultsPath, fileFormat);

    AdvancedConsole.WriteLine($"Лог-процессоров:{Environment.ProcessorCount}", ConsoleMessageType.Warning);

    await tokenizer.TrainAsync(inputFilePath,
        "<|endoftext|>",
        parsedVocabSize,
        Environment.ProcessorCount);

    await tokenizer.SaveVocabAsync();
}*/

var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
var trainResultsPath = Path.Combine(baseDirectory, "TrainResults");
var tokenizer = new BPETokenizer(trainResultsPath, VocabFileFormat.MessagePack);

while (true)
{
    AdvancedConsole.Clear();
    AdvancedConsole.WriteMenu("Загрузить словарь",
        "Обучить с нуля",
        "Выход");

    var userInput = Console.ReadLine()?.Trim().ToUpper();

    if (userInput == "3")
    {
        AdvancedConsole.WriteLine("Завершение программы...");
        break;
    }

    if (userInput == "1")
    {
        await tokenizer.LoadVocabAsync();

        AdvancedConsole.WriteLine("Словарь загружен.", ConsoleMessageType.Success);

        AdvancedConsole.WriteLine("Нажмите любую клавишу...", ConsoleMessageType.Debug);
        AdvancedConsole.ReadKey();

        await RunOperationsSubMenu();
    }
    else if (userInput == "2")
    {
        AdvancedConsole.Write("Введите желаемый размер словаря:", ConsoleMessageType.Debug);
        var vocabSizeStr = Console.ReadLine();

        var vocabSize = 0;
        if (int.TryParse(vocabSizeStr, out var parsedVocabSize))
        {
            vocabSize = parsedVocabSize;
        }
        else
        {
            AdvancedConsole.WriteLine("Неверный ввод. Нажмите любую клавишу для продолжения...", ConsoleMessageType.Error);
            AdvancedConsole.ReadKey();
        }

        AdvancedConsole.Write("Введите адрес датасэта:", ConsoleMessageType.Debug);
        var inputFilePath = Console.ReadLine();
        if (inputFilePath.IsNullOrEmpty())
        {
            AdvancedConsole.WriteLine($"Некорректный пусть к датасету. Пожалуйсте, введите полный путь до файла.", ConsoleMessageType.Error);
            AdvancedConsole.ReadKey();
        }

        await tokenizer.TrainAsync(inputFilePath,
            "<|endoftext|>",
            parsedVocabSize,
            Environment.ProcessorCount);

        AdvancedConsole.WriteLine("Нажмите любую клавишу...", ConsoleMessageType.Debug);
        AdvancedConsole.ReadKey();

        // Переходим в подменю операций
        await RunOperationsSubMenu();
    }
    else
    {
        AdvancedConsole.WriteLine("Неверный ввод. Нажмите любую клавишу для продолжения...", ConsoleMessageType.Error);
        AdvancedConsole.ReadKey();
    }
}

async Task RunOperationsSubMenu()
{
    while (true)
    {
        AdvancedConsole.Clear();
        AdvancedConsole.WriteMenu("Кодировать текст",
            "Декодировать текст",
            "Вернуться в главное меню");

        var userInput = Console.ReadLine()?.Trim();

        if (userInput == "1")
        {
            AdvancedConsole.Write($"Введите кодируемый текст:", ConsoleMessageType.Warning);

            var text = Console.ReadLine();
            var encodedText = await tokenizer.EncodeAsync(text);

            AdvancedConsole.WriteLine($"Кодировка: [{string.Join(',', encodedText)}]",
                ConsoleMessageType.Success);

            AdvancedConsole.WriteLine("Нажмите любую клавишу...", ConsoleMessageType.Debug);
            AdvancedConsole.ReadKey();
        }
        else if (userInput == "2")
        {
            AdvancedConsole.Write($"Введите декодируемый массив:", ConsoleMessageType.Warning);
            var encoded = Console.ReadLine();

            var decodetText = tokenizer.Decode(ParseIntsFromString(encoded));

            AdvancedConsole.Write($"Декодировано: {decodetText}", ConsoleMessageType.Success);

            AdvancedConsole.WriteLine("Нажмите любую клавишу...", ConsoleMessageType.Debug);
            AdvancedConsole.ReadKey();
        }
        else if (userInput == "3")
        {
            // Возврат в главное меню
            break;
        }
        else
        {
            AdvancedConsole.WriteLine("Неверный ввод. Нажмите любую клавишу...", ConsoleMessageType.Error);
            AdvancedConsole.ReadKey();
        }
    }
}

IEnumerable<int> ParseIntsFromString(string input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return Enumerable.Empty<int>();
    }

    var parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

    var result = new List<int>();
    foreach (var part in parts)
    {
        if (int.TryParse(part, out var number))
        {
            result.Add(number);
        }
    }
    return result;
}