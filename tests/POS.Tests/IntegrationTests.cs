using POS.Application;
using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Application.Services;
using POS.Domain.Services;
using POS.Domain.Shared;
using POS.Infrastructure.Export;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Tests;

public sealed class IntegrationTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("pos-tests-");
    private string DataPath => Path.Combine(_directory.FullName, "pos.json");

    [Fact]
    public async Task DraftAllowsPreviewUndoAndCancellationBeforeSave()
    {
        var pos = Operations();
        await pos.StartRubricSetupAsync();
        await Assert.ThrowsAsync<DomainException>(pos.FinishRubricSetupAsync);

        var first = pos.AddDraftCriterion(new CriterionSpec("Практика", 20, 1.25m));
        pos.AddDraftCriterion(new CriterionSpec("Семинар", 6, 1));
        pos.UndoLastDraftCriterion();
        Assert.Equal(first, Assert.Single(pos.PreviewRubricDraft()));
        Assert.False(File.Exists(DataPath));
        pos.CancelRubricSetup();
        Assert.Null(await pos.GetRubricAsync());

        var current = await SetUpAsync(pos);
        var reopened = await Operations().GetRubricAsync();
        Assert.NotNull(reopened);
        Assert.Equal(current, Assert.Single(reopened.Criteria));
        await Assert.ThrowsAsync<DomainException>(pos.StartRubricSetupAsync);
    }

    [Fact]
    public async Task MaximumChangesAllAffectedGradesWithOneSave()
    {
        var repository = new CountingRepository(new JsonPosRepository(DataPath));
        var pos = Operations(repository);
        var first = await pos.AddStudentAsync(Student("БПИ241"));
        var second = await pos.AddStudentAsync(Student("БПИ242"));
        var third = await pos.AddStudentAsync(Student("БПИ243"));
        var criterion = await SetUpAsync(pos);

        await pos.RecordEvaluationAsync(new(first.Id, criterion.Id, 8));
        await pos.RecordEvaluationAsync(new(second.Id, criterion.Id, 9));
        await pos.RecordEvaluationAsync(new(third.Id, criterion.Id, 3.25m));
        var saves = repository.Saves;

        var result = await pos.ChangeMaxScoreAsync(criterion.Id, 5);
        Assert.Equal(saves + 1, repository.Saves);
        Assert.Equal(2, result.AdjustedEvaluations);
        var reopened = Operations();
        Assert.Equal(5m, Assert.Single(await reopened.ListEvaluationsAsync(first.Id)).Score);
        Assert.Equal(5m, Assert.Single(await reopened.ListEvaluationsAsync(second.Id)).Score);
        Assert.Equal(3.25m, Assert.Single(await reopened.ListEvaluationsAsync(third.Id)).Score);
        Assert.Equal(5m, Assert.Single((await reopened.GetRubricAsync())!.Criteria).Data.MaxScore);
        Assert.Equal(0, (await reopened.ChangeMaxScoreAsync(criterion.Id, 20)).AdjustedEvaluations);
        Assert.Equal(5m, Assert.Single(await reopened.ListEvaluationsAsync(first.Id)).Score);
    }

    [Fact]
    public async Task InvalidChangesDoNotSaveAndReferencesPreventRemoval()
    {
        var repository = new CountingRepository(new JsonPosRepository(DataPath));
        var pos = Operations(repository);
        var student = await pos.AddStudentAsync(Student("БПИ241"));
        var criterion = await SetUpAsync(pos);
        var saves = repository.Saves;
        var state = await File.ReadAllTextAsync(DataPath);

        await Assert.ThrowsAsync<DomainException>(() => pos.RecordEvaluationAsync(new(student.Id, criterion.Id, 12)));
        await Assert.ThrowsAsync<DomainException>(() => pos.ChangeMaxScoreAsync(criterion.Id, 0));
        Assert.Equal(saves, repository.Saves);
        Assert.Equal(state, await File.ReadAllTextAsync(DataPath));

        await pos.RecordEvaluationAsync(new(student.Id, criterion.Id, 8));
        Assert.Equal(ErrorCode.StudentInUse,
            (await Assert.ThrowsAsync<DomainException>(() => pos.RemoveStudentAsync(student.Id))).Code);
        Assert.Equal(ErrorCode.CriterionInUse,
            (await Assert.ThrowsAsync<DomainException>(() => pos.RemoveCriterionAsync(criterion.Id))).Code);

        await pos.CancelEvaluationAsync(student.Id, criterion.Id);
        await pos.RemoveCriterionAsync(criterion.Id);
        var empty = await pos.CalculateFinalAsync(student.Id);
        Assert.Null(empty.Result.RoundedValue);
        Assert.Equal(0, empty.Result.TotalCriteria);
        await pos.RemoveStudentAsync(student.Id);
        Assert.Empty(await pos.ListStudentsAsync());
    }

    [Theory]
    [InlineData("{ invalid")]
    [InlineData("{\"schemaVersion\":1,\"students\":[null],\"rubric\":null,\"gradebook\":null}")]
    [InlineData("{\"schemaVersion\":2,\"students\":[],\"rubric\":null,\"gradebook\":null}")]
    public async Task InvalidJsonIsReportedWithoutOverwritingIt(string invalid)
    {
        await File.WriteAllTextAsync(DataPath, invalid);
        var error = await Assert.ThrowsAsync<PosFileException>(() => Operations().AddStudentAsync(Student("БПИ241")));
        Assert.Equal(FileError.DataCorrupted, error.Code);
        Assert.Contains(DataPath, error.Message);
        Assert.Equal(invalid, await File.ReadAllTextAsync(DataPath));
    }

    [Fact]
    public async Task FilterAndExportPreserveCyrillicAndDistinguishMissingGrade()
    {
        var pos = Operations();
        var first = await pos.AddStudentAsync(Student("=БПИ241") with { EducationalProgram = "ПИ, \"трек\"" });
        var second = await pos.AddStudentAsync(Student("БПИ231") with { StudyYear = 3 });
        var criterion = await SetUpAsync(pos);
        await pos.RecordEvaluationAsync(new(second.Id, criterion.Id, 4.25m));

        var filter = new StudentFilter("ёлкина", "пи, \"трек\"", 2, "=бпи241");
        Assert.Equal(first, Assert.Single(await Operations().ListStudentsAsync(filter)));
        var path = Path.Combine(_directory.FullName, "results.csv");
        Assert.Equal(path, await pos.ExportResultsAsync(path, filter));
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, (await File.ReadAllBytesAsync(path))[..3]);
        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal(2, lines.Length);
        Assert.Equal("StudentId,LastName,FirstName,MiddleName,StudyYear,EducationalProgram,AcademicGroup,RawFinal,RoundedFinal,Status,GradedCriteria,TotalCriteria", lines[0]);
        Assert.Contains("Ёлкина,Анна-Мария,,2,\"ПИ, \"\"трек\"\"\",'=БПИ241,,,NoResult,0,1", lines[1]);
        Assert.Equal(4.25m, Assert.Single(await Operations().ListEvaluationsAsync(second.Id)).Score);

        var state = await File.ReadAllTextAsync(DataPath);
        var failed = await Assert.ThrowsAsync<PosFileException>(() => pos.ExportResultsAsync(DataPath));
        Assert.Equal(FileError.ExportError, failed.Code);
        Assert.Equal(state, await File.ReadAllTextAsync(DataPath));
    }

    [Fact]
    public async Task CanceledOrFailedWritePreservesExistingDestination()
    {
        var repository = new JsonPosRepository(DataPath);
        await Operations(repository).AddStudentAsync(Student("БПИ241"));
        var previous = await File.ReadAllBytesAsync(DataPath);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.SaveAsync(new PosSnapshot(), new CancellationToken(canceled: true)));
        Assert.Equal(previous, await File.ReadAllBytesAsync(DataPath));

        var impossible = new JsonPosRepository(Path.Combine(DataPath, "pos.json"));
        var error = await Assert.ThrowsAsync<PosFileException>(() => impossible.SaveAsync(new PosSnapshot()));
        Assert.Equal(FileError.StorageError, error.Code);
        Assert.Equal(previous, await File.ReadAllBytesAsync(DataPath));
        Assert.Empty(_directory.GetFiles("*.tmp"));
    }

    private IPosOperations Operations(IPosRepository? repository = null)
    {
        repository ??= new JsonPosRepository(DataPath);
        var assessment = new AssessmentService(repository);
        var strategies = new Dictionary<RoundingMode, IRoundingStrategy>
        {
            [RoundingMode.HalfUp] = new HalfUpRoundingStrategy(),
            [RoundingMode.Floor] = new FloorRoundingStrategy()
        };
        return new PosFacade(new StudentService(repository), assessment, new ReportingService(repository,
            new CsvResultExporter(DataPath), new FinalScoreCalculator(), strategies), new RubricSetupSession(assessment));
    }

    private static AddStudentRequest Student(string group) =>
        new("Анна-Мария", "Ёлкина", null, 2, "Программная инженерия", group);

    private static async Task<CriterionView> SetUpAsync(IPosOperations pos)
    {
        await pos.StartRubricSetupAsync();
        var criterion = pos.AddDraftCriterion(new CriterionSpec("Проект", 10, 1.25m));
        await pos.FinishRubricSetupAsync();
        return criterion;
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private sealed class CountingRepository(IPosRepository inner) : IPosRepository
    {
        public int Saves { get; private set; }
        public Task<PosSnapshot> LoadAsync(CancellationToken token = default) => inner.LoadAsync(token);

        public async Task SaveAsync(PosSnapshot snapshot, CancellationToken token = default)
        {
            await inner.SaveAsync(snapshot, token);
            Saves++;
        }
    }
}
