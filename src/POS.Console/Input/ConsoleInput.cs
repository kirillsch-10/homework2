using System.Globalization;

namespace POS.ConsoleApp.Input;

internal static class ConsoleInput
{
    public static string Read(string label)
    {
        Console.Write(label + ": ");
        return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
    }

    public static int Number(string label, int min, int max)
    {
        while (true)
        {
            if (int.TryParse(Read(label), NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                value >= min && value <= max)
                return value;
            Console.WriteLine($"Введите целое число от {min} до {max}.");
        }
    }

    public static int? OptionalYear(string label)
    {
        while (true)
        {
            var input = Read(label);
            if (input.Length == 0)
                return null;
            if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 6)
                return value;
            Console.WriteLine("Введите год обучения от 1 до 6 или оставьте поле пустым.");
        }
    }

    public static decimal Decimal(string label)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        while (true)
        {
            if (decimal.TryParse(Read(label).Replace(',', '.'), styles, CultureInfo.InvariantCulture, out var value))
                return value;
            Console.WriteLine("Введите число, например 7 или 7,5.");
        }
    }

    public static string TextField(string label, string? current = null)
    {
        var value = Read(current is null ? label : $"{label} [{current}]");
        return value.Length == 0 && current is not null ? current : value;
    }

    public static bool Confirm(string question) => Read(question + " [д/Н]").ToLowerInvariant() is "д" or "да";
}
