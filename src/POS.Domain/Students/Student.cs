using System.Text.RegularExpressions;
using POS.Domain.Shared;

namespace POS.Domain.Students;

public readonly record struct StudentId
{
    public Guid Value { get; }
    public StudentId(Guid value) => Value = Rules.Id(value);
    public static StudentId New() => new(Guid.NewGuid());
}

public sealed record FullName
{
    private static readonly Regex Pattern = new(
        @"\A(?:[A-Z][a-z]*|[А-ЯЁ][а-яё]*)(?:[ -](?:[A-Z][a-z]*|[А-ЯЁ][а-яё]*))*\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public string FirstName { get; }
    public string LastName { get; }
    public string? MiddleName { get; }

    public FullName(string firstName, string lastName, string? middleName = null)
    {
        FirstName = Part(firstName, "Имя");
        LastName = Part(lastName, "Фамилия");
        MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : Part(middleName, "Отчество");
    }

    private static string Part(string? value, string label)
    {
        var text = value?.Trim() ?? "";
        if (!Pattern.IsMatch(text))
            throw new DomainException($"{label}: в каждом слове первая буква заглавная, остальные строчные, одного алфавита.");
        return text;
    }

    public override string ToString() => MiddleName is null
        ? $"{LastName} {FirstName}" : $"{LastName} {FirstName} {MiddleName}";
}

public sealed record StudyYear
{
    public int Value { get; }

    public StudyYear(int value)
    {
        if (value is < 1 or > 6)
            throw new DomainException("Год обучения должен быть от 1 до 6.");
        Value = value;
    }
}

public sealed class Student
{
    public StudentId Id { get; }
    public FullName Name { get; private set; }
    public StudyYear Year { get; private set; }
    public string EducationalProgram { get; private set; }
    public string AcademicGroup { get; private set; }

    public Student(StudentId id, FullName name, StudyYear year, string program, string group)
    {
        Rules.Id(id.Value);
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Year = year ?? throw new ArgumentNullException(nameof(year));
        EducationalProgram = Rules.Text(program, 120, "Образовательная программа");
        AcademicGroup = Rules.Text(group, 40, "Академическая группа");
    }

    public void Update(FullName name, StudyYear year, string program, string group)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(year);
        var validProgram = Rules.Text(program, 120, "Образовательная программа");
        var validGroup = Rules.Text(group, 40, "Академическая группа");
        Name = name;
        Year = year;
        EducationalProgram = validProgram;
        AcademicGroup = validGroup;
    }
}
