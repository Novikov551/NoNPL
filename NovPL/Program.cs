using NoNPL;
using NoNPL.Services.Serializers;
Console.OutputEncoding = System.Text.Encoding.UTF8;

class Programm
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            AdvancedConsole.WriteLine("Specify the path to dataset.", ConsoleMessageType.Error);
            return;
        }

        var inputFilePath = args[0];

        var pattern = @"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+";

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var trainResultsPath = Path.Combine(baseDirectory, "TrainResults");

        var tokenizer = new BPETokenizer(pattern, trainResultsPath, VocabFileFormat.MessagePack);

        await tokenizer.TrainAsync(inputFilePath,
            "<|endoftext|>",
            10000);
    }
}






/*AdvancedConsole.WriteLine("Обучение завершено! Нажмите любую клавишу для продолжения...");
Console.ReadKey(true);

Console.Clear();

Console.Write("Введите кодируемый текст -> ");
string? text = Console.ReadLine()?.Trim();

if (string.IsNullOrEmpty(text))
{
    text = " ";
}

Console.Clear();

var result = await tokenizer.Encode(text);

Console.WriteLine("╔════════════════════════════════════╗");
Console.WriteLine("║         РЕЗУЛЬТАТ КОДИРОВАНИЯ      ║");
Console.WriteLine("╚════════════════════════════════════╝");
Console.WriteLine($"Исходный текст: \"{text}\"");
Console.WriteLine($"Результат: [ {string.Join(", ", result)} ]");
Console.WriteLine(new string('─', 60));
Console.WriteLine();

int searchResultLine = Console.CursorTop;
Console.WriteLine();
int inputLine = Console.CursorTop;

while (true)
{
    Console.SetCursorPosition(0, inputLine);
    Console.Write(new string(' ', Console.WindowWidth));
    Console.SetCursorPosition(0, inputLine);
    Console.Write("Введите число для поиска (или нажмите 'C' для завершения): ");

    // Читаем клавишу без ожидания Enter
    var keyInfo = Console.ReadKey(intercept: true);

    // Проверяем, нажата ли клавиша C (без учёта регистра)
    if (keyInfo.Key == ConsoleKey.C)
    {
        // Выходим из цикла при нажатии C
        break;
    }

    // Если нажата не C, то обрабатываем ввод числа
    // Для ввода числа нам нужна полная строка, поэтому используем обычный ReadLine
    // Но сначала нужно вернуть курсор и показать нажатую клавишу
    Console.SetCursorPosition(0, inputLine);
    Console.Write(new string(' ', Console.WindowWidth));
    Console.SetCursorPosition(0, inputLine);
    Console.Write("Введите число для поиска (или нажмите 'C' для завершения): ");

    // Показываем первый символ
    Console.Write(keyInfo.KeyChar);

    // Теперь читаем остаток строки
    string? restOfLine = Console.ReadLine();
    string input = keyInfo.KeyChar.ToString() + (restOfLine ?? "");

    Console.SetCursorPosition(0, searchResultLine);
    Console.Write(new string(' ', Console.WindowWidth));
    Console.SetCursorPosition(0, searchResultLine + 1);
    Console.Write(new string(' ', Console.WindowWidth));

    Console.SetCursorPosition(0, searchResultLine);

    if (int.TryParse(input, out int value))
    {
        var token = tokenizer.GetToken(value);
        if (token != null)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Значение {value} → Токен: '{token.ToString()}'");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Значение {value} не найдено в словаре");
            Console.ResetColor();
        }
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠️ Ошибка: '{input}' не является целым числом");
        Console.ResetColor();
    }
}
Console.WriteLine();
// Программа доходит до этих строк при нажатии C
Console.WriteLine(" Нажмите любую клавишу для продолжения...");
Console.ReadKey(true);

Console.Clear();


Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════╗");
Console.WriteLine("║         ДЕКОДИРОВАНИЕ ТОКЕНОВ       ║");
Console.WriteLine("╚════════════════════════════════════╝");
Console.WriteLine("Введите последовательность чисел через пробел для декодирования");
Console.WriteLine("(или нажмите 'C' для выхода):");

while (true)
{
    Console.Write("➤ ");
    string? input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
        continue;

    // Проверка на выход
    if (input.Equals("C", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Разбираем введенные числа
    string[] parts = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
    List<int> tokenIds = new List<int>();
    bool hasErrors = false;

    foreach (string part in parts)
    {
        if (int.TryParse(part, out int id))
        {
            tokenIds.Add(id);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Ошибка: '{part}' не является целым числом");
            Console.ResetColor();
            hasErrors = true;
            break;
        }
    }

    if (hasErrors || tokenIds.Count == 0)
        continue;

    Console.Clear();

    Console.WriteLine("╔════════════════════════════════════╗");
    Console.WriteLine("║         РЕЗУЛЬТАТ ДЕКОДИРОВАНИЯ    ║");
    Console.WriteLine("╚════════════════════════════════════╝");

    // Показываем введенные ID
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Входные ID: [{string.Join(", ", tokenIds)}]");
    Console.ResetColor();
    Console.WriteLine(new string('─', 60));

    // Показываем соответствующие токены
    Console.WriteLine("Соответствующие токены:");
    for (int i = 0; i < tokenIds.Count; i++)
    {
        var token = tokenizer.GetToken(tokenIds[i]);
        if (token != null)
        {
            Console.WriteLine($"  {tokenIds[i],4} → '{token.ToString()}'");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  {tokenIds[i],4} → [ТОКЕН НЕ НАЙДЕН]");
            Console.ResetColor();
        }
    }
    Console.WriteLine(new string('─', 60));

    // Декодируем и показываем результат
    string decodedText = tokenizer.Decode(tokenIds);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Декодированный текст:");
    Console.ResetColor();
    Console.WriteLine($"\"{decodedText}\"");

    // Проверяем, были ли замены символов
    if (decodedText.Contains('\uFFFF'))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠️ Внимание: Некоторые байты не удалось декодировать в UTF-8 и они были заменены на символ U+FFFF");
        Console.ResetColor();
    }

    Console.WriteLine(new string('═', 60));
    Console.WriteLine();
}

Console.WriteLine(" Нажмите любую клавишу для выхода...");
Console.ReadKey(true);
Console.Clear();*/