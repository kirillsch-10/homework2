using POS.Application.Contracts;

namespace POS.Application.Ports;

public interface IResultExporter
{
    Task<string> ExportAsync(string path, IReadOnlyList<ResultRow> rows, CancellationToken cancellationToken = default);
}
