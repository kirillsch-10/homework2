using System.Globalization;
using System.Text;
using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Export;

public sealed class CsvResultExporter(string dataPath) : IResultExporter
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: true);
    private readonly string _dataPath = Path.GetFullPath(dataPath);

    public async Task<string> ExportAsync(string path, IReadOnlyList<ResultRow> rows, CancellationToken cancellationToken = default)
    {
        try
        {
            var destination = Path.GetFullPath(path);
            var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (string.Equals(destination, _dataPath, comparison))
                throw new IOException("Укажите для CSV путь, отличный от пути файла данных.");

            var csv = new StringBuilder("StudentId,LastName,FirstName,MiddleName,StudyYear,EducationalProgram,AcademicGroup,"
                + "RawFinal,RoundedFinal,Status,GradedCriteria,TotalCriteria\r\n");
            foreach (var (student, result) in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = student.Data;
                csv.AppendJoin(',', new[]
                {
                    student.Id.ToString(), Escape(data.LastName), Escape(data.FirstName), Escape(data.MiddleName),
                    data.StudyYear.ToString(Culture), Escape(data.EducationalProgram), Escape(data.AcademicGroup),
                    result.RawValue?.ToString("G29", Culture) ?? "", result.RoundedValue?.ToString(Culture) ?? "",
                    result.Status.ToString(), result.GradedCriteria.ToString(Culture), result.TotalCriteria.ToString(Culture)
                }).Append("\r\n");
            }

            var bytes = Encoding.GetPreamble().Concat(Encoding.GetBytes(csv.ToString())).ToArray();
            await AtomicFile.WriteAsync(destination, bytes, cancellationToken);
            return destination;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PosFileException(FileError.ExportError, $"Не удалось сохранить CSV «{path}». {ex.Message}", ex);
        }
    }

    private static string Escape(string? field)
    {
        var value = field ?? "";
        if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
