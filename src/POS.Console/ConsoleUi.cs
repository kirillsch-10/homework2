using System.Globalization;
using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Domain.Services;
using POS.Domain.Shared;
using static POS.ConsoleApp.Input.ConsoleInput;

namespace POS.ConsoleApp;

public sealed class ConsoleUi(IPosOperations pos)
{
    private RoundingMode _rounding = RoundingMode.HalfUp;

    public async Task RunAsync()
    {
        try
        {
            await MenuAsync("ПОС — преподавательская оценка студентов", "Выход",
                ("Студенты", StudentsAsync), ("Рубрикатор", RubricAsync),
                ("Оценки", GradesAsync), ("Итоги и экспорт", ReportsAsync));
        }
        catch (EndOfStreamException) { Console.WriteLine(); }
    }

    private Task StudentsAsync() => MenuAsync("Студенты", "Назад",
        ("Добавить", async () =>
        {
            var student = await pos.AddStudentAsync(ReadStudent());
            Console.WriteLine($"Студент {student.FullName} добавлен.");
        }),
        ("Показать всех", async () => Print(await pos.ListStudentsAsync(), StudentLabel)),
        ("Найти", async () => Print(await pos.ListStudentsAsync(ReadFilter()), StudentLabel)),
        ("Изменить", async () =>
        {
            if (await PickStudentAsync() is not { } student)
                return;
            await pos.UpdateStudentAsync(student.Id, ReadStudent(student.Data));
            Console.WriteLine("Данные студента сохранены.");
        }),
        ("Удалить", async () =>
        {
            if (await PickStudentAsync() is not { } student || !Confirm($"Удалить {student.FullName}?"))
                return;
            await pos.RemoveStudentAsync(student.Id);
            Console.WriteLine("Студент удалён.");
        }));

    private Task RubricAsync() => MenuAsync("Рубрикатор", "Назад",
        ("Первичная настройка", SetupAsync),
        ("Показать критерии", async () => Print(await GetCriteriaAsync(), CriterionLabel)),
        ("Добавить критерий", async () =>
        {
            await GetCriteriaAsync();
            Console.WriteLine(CriterionLabel(await pos.AddCriterionAsync(ReadCriterion())));
        }),
        ("Переименовать критерий", async () =>
        {
            if (await PickCriterionAsync() is { } criterion)
                Console.WriteLine(CriterionLabel(await pos.RenameCriterionAsync(criterion.Id, Read("Новое название"))));
        }),
        ("Изменить максимум", async () =>
        {
            if (await PickCriterionAsync() is not { } criterion)
                return;
            var maximum = Decimal("Новый максимум");
            if (maximum > 0 && maximum < criterion.Data.MaxScore &&
                !Confirm("Оценки выше нового максимума будут снижены. Продолжить?"))
                return;

            var result = await pos.ChangeMaxScoreAsync(criterion.Id, maximum);
            Console.WriteLine(CriterionLabel(result.Criterion));
            Console.WriteLine($"Снижено оценок: {result.AdjustedEvaluations}.");
        }),
        ("Изменить вес", async () =>
        {
            if (await PickCriterionAsync() is { } criterion)
                Console.WriteLine(CriterionLabel(await pos.ChangeWeightAsync(criterion.Id, Decimal("Новый вес"))));
        }),
        ("Удалить критерий", async () =>
        {
            if (await PickCriterionAsync() is not { } criterion || !Confirm($"Удалить «{criterion.Data.Name}»?"))
                return;
            await pos.RemoveCriterionAsync(criterion.Id);
            Console.WriteLine("Критерий удалён.");
        }));

    private async Task SetupAsync()
    {
        await pos.StartRubricSetupAsync();
        var finished = false;
        try
        {
            await MenuAsync("Черновик рубрикатора", "Отменить настройку",
                [
                    ("Добавить критерий", () =>
                    {
                        Console.WriteLine(CriterionLabel(pos.AddDraftCriterion(ReadCriterion())));
                        return Task.CompletedTask;
                    }),
                    ("Предпросмотр", () =>
                    {
                        Print(pos.PreviewRubricDraft(), CriterionLabel);
                        return Task.CompletedTask;
                    }),
                    ("Откатить последний шаг", () =>
                    {
                        pos.UndoLastDraftCriterion();
                        Console.WriteLine("Последний критерий удалён из черновика.");
                        return Task.CompletedTask;
                    }),
                    ("Сохранить рубрикатор", async () =>
                    {
                        if (!Confirm("Сохранить рубрикатор?"))
                            return;
                        var saved = await pos.FinishRubricSetupAsync();
                        Console.WriteLine($"Рубрикатор сохранён. Критериев: {saved.Criteria.Count}.");
                        finished = true;
                    })
                ],
                stop: () => finished);
        }
        finally
        {
            if (!finished)
                pos.CancelRubricSetup();
        }
    }

    private Task GradesAsync() => MenuAsync("Оценки", "Назад",
        ("Выставить", () => WriteGradeAsync(pos.RecordEvaluationAsync)),
        ("Посмотреть", async () =>
        {
            if (await PickStudentAsync() is { } student)
                Print(await pos.ListEvaluationsAsync(student.Id), EvaluationLabel);
        }),
        ("Исправить", () => WriteGradeAsync(pos.CorrectEvaluationAsync)),
        ("Отменить", async () =>
        {
            if (await PickStudentAsync() is not { } student || await PickCriterionAsync() is not { } criterion ||
                !Confirm("Отменить выставленную оценку?"))
                return;
            await pos.CancelEvaluationAsync(student.Id, criterion.Id);
            Console.WriteLine("Оценка отменена.");
        }));

    private async Task WriteGradeAsync(Func<RecordEvaluationRequest, Task<EvaluationView>> write)
    {
        if (await PickStudentAsync() is not { } student || await PickCriterionAsync() is not { } criterion)
            return;
        var score = Decimal($"Балл от 0 до {Format(criterion.Data.MaxScore)}");
        Console.WriteLine(EvaluationLabel(await write(new RecordEvaluationRequest(student.Id, criterion.Id, score))));
    }

    private Task ReportsAsync() => MenuAsync("Итоги и экспорт", "Назад",
        ("Итог студента", async () =>
        {
            if (await PickStudentAsync() is { } student)
                Console.WriteLine(ResultLabel(await pos.CalculateFinalAsync(student.Id, _rounding)));
        }),
        ("Ведомость", async () => Print(await pos.ListResultsAsync(ReadFilter(), _rounding), ResultLabel)),
        ("Экспорт CSV", async () =>
        {
            var filter = ReadFilter();
            var path = Read("Путь к CSV [results.csv]");
            var output = await pos.ExportResultsAsync(path.Length == 0 ? "results.csv" : path, filter, _rounding);
            Console.WriteLine($"Ведомость сохранена в «{output}».");
        }),
        ("Округление", () =>
        {
            Console.WriteLine("1. До ближайшего целого, половины вверх   2. Вниз до целого");
            _rounding = Number("Режим", 1, 2) == 1 ? RoundingMode.HalfUp : RoundingMode.Floor;
            Console.WriteLine(_rounding == RoundingMode.HalfUp ? "Выбрано округление половин вверх." : "Выбрано округление вниз.");
            return Task.CompletedTask;
        }));

    private async Task<StudentView?> PickStudentAsync() => Pick(await pos.ListStudentsAsync(), StudentLabel);
    private async Task<CriterionView?> PickCriterionAsync() => Pick(await GetCriteriaAsync(), CriterionLabel);

    private async Task<IReadOnlyList<CriterionView>> GetCriteriaAsync() =>
        (await pos.GetRubricAsync())?.Criteria
            ?? throw new DomainException("Сначала настройте рубрикатор.", ErrorCode.RubricNotInitialized);

    private static T? Pick<T>(IReadOnlyList<T> values, Func<T, string> label) where T : class
    {
        if (values.Count == 0)
        {
            Console.WriteLine("Список пуст.");
            return null;
        }
        for (var i = 0; i < values.Count; i++)
            Console.WriteLine($"{i + 1}. {label(values[i])}");
        Console.WriteLine("0. Назад");
        var choice = Number("Выбор", 0, values.Count);
        return choice == 0 ? null : values[choice - 1];
    }

    private static Task MenuAsync(string title, string exit, params (string Label, Func<Task> Run)[] options) =>
        MenuAsync(title, exit, options, () => false);

    private static async Task MenuAsync(string title, string exit, (string Label, Func<Task> Run)[] options, Func<bool> stop)
    {
        while (!stop())
        {
            Console.WriteLine($"\n{title}");
            for (var i = 0; i < options.Length; i++)
                Console.WriteLine($"{i + 1}. {options[i].Label}");
            Console.WriteLine($"0. {exit}");
            var choice = Number("Действие", 0, options.Length);
            if (choice == 0)
                return;
            try { await options[choice - 1].Run(); }
            catch (Exception ex) when (ex is DomainException or PosFileException)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }

    private static AddStudentRequest ReadStudent(AddStudentRequest? current = null)
    {
        var last = TextField("Фамилия", current?.LastName);
        var first = TextField("Имя", current?.FirstName);
        var middle = TextField(current is null ? "Отчество (необязательно)" : "Отчество («-» для удаления)", current?.MiddleName);
        var year = current is null ? Number("Год обучения", 1, 6)
            : OptionalYear($"Год обучения [{current.StudyYear}]") ?? current.StudyYear;
        return new AddStudentRequest(first, last, middle == "-" && current is not null ? null : middle, year,
            TextField("Образовательная программа", current?.EducationalProgram),
            TextField("Академическая группа", current?.AcademicGroup));
    }

    private static CriterionSpec ReadCriterion() =>
        new(Read("Название"), Decimal("Максимальный балл"), Decimal("Вес"));

    private static StudentFilter ReadFilter()
    {
        Console.WriteLine("Оставьте поле пустым, чтобы пропустить фильтр.");
        return new StudentFilter(Read("Часть ФИО"), Read("Образовательная программа"),
            OptionalYear("Год обучения"), Read("Академическая группа"));
    }

    private static void Print<T>(IReadOnlyList<T> values, Func<T, string> label)
    {
        if (values.Count == 0)
            Console.WriteLine("Список пуст.");
        foreach (var value in values)
            Console.WriteLine(label(value));
    }

    private static string StudentLabel(StudentView s) =>
        $"{s.FullName} · {s.Data.EducationalProgram} · {s.Data.StudyYear} год · {s.Data.AcademicGroup} · {s.Id}";

    private static string CriterionLabel(CriterionView c) =>
        $"{c.Data.Name} · максимум {Format(c.Data.MaxScore)} · вес {Format(c.Data.Weight)}";

    private static string EvaluationLabel(EvaluationView e) =>
        $"{e.Criterion.Data.Name}: {Format(e.Score)} / {Format(e.Criterion.Data.MaxScore)}.";

    private static string ResultLabel(ResultRow row)
    {
        var result = row.Result;
        var label = result.Status switch
        {
            FinalScoreStatus.Final => "окончательный итог",
            FinalScoreStatus.Preliminary => "предварительный итог",
            _ => result.TotalCriteria == 0 ? "критериев пока нет" : "итога пока нет"
        };
        var score = result.RawValue is { } raw && result.RoundedValue is { } rounded
            ? $" {Format(rounded)}/10 (точный {Format(raw)})" : "";
        return $"{StudentLabel(row.Student)} · {label}{score} · критерии {result.GradedCriteria}/{result.TotalCriteria}";
    }

    private static string Format(decimal value) => value.ToString("G29", CultureInfo.GetCultureInfo("ru-RU"));
}
