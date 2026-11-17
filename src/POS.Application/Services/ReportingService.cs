using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Domain.Services;
using POS.Domain.Shared;
using POS.Domain.Students;

namespace POS.Application.Services;

public sealed class ReportingService(IPosRepository repository, IResultExporter exporter,
    FinalScoreCalculator calculator, IReadOnlyDictionary<RoundingMode, IRoundingStrategy> strategies)
{
    public async Task<ResultRow> CalculateAsync(Guid id, RoundingMode mode)
    {
        var snapshot = await repository.LoadAsync();
        return Row(snapshot, snapshot.Get(new StudentId(id)), Strategy(mode));
    }

    public async Task<IReadOnlyList<ResultRow>> ListAsync(StudentFilter? filter, RoundingMode mode)
    {
        var strategy = Strategy(mode);
        var snapshot = await repository.LoadAsync();
        return StudentService.Select(snapshot, filter).Select(student => Row(snapshot, student, strategy)).ToArray();
    }

    public async Task<string> ExportAsync(string path, StudentFilter? filter, RoundingMode mode) =>
        await exporter.ExportAsync(path, await ListAsync(filter, mode));

    private IRoundingStrategy Strategy(RoundingMode mode) =>
        strategies.GetValueOrDefault(mode) ?? throw new DomainException("Неизвестный режим округления.");

    private ResultRow Row(PosSnapshot snapshot, Student student, IRoundingStrategy rounding)
    {
        if (snapshot.Rubric is null)
            return new ResultRow(StudentService.View(student), new(null, null, FinalScoreStatus.NoResult, 0, 0));
        var (rubric, book) = snapshot.Assessment();
        return new ResultRow(StudentService.View(student), calculator.Calculate(student.Id, rubric, book, rounding));
    }
}
