using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using POS.Application.Contracts;
using POS.Application.Ports;
using POS.Domain.Shared;

namespace POS.Infrastructure.Persistence;

public sealed class JsonPosRepository(string path) : IPosRepository
{
    private readonly string _path = Path.GetFullPath(path);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic)
    };

    public async Task<PosSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(_path, cancellationToken);
            var dto = JsonSerializer.Deserialize<PosFileDto>(data, Options)
                ?? throw new JsonException("В файле нет объекта состояния.");
            return PosFileMapper.Restore(dto);
        }
        catch (FileNotFoundException) { return new PosSnapshot(); }
        catch (DirectoryNotFoundException) { return new PosSnapshot(); }
        catch (Exception ex) when (ex is JsonException or DomainException)
        {
            throw new PosFileException(FileError.DataCorrupted,
                $"Файл данных «{_path}» повреждён или имеет неподдерживаемый формат. {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new PosFileException(FileError.StorageError, $"Не удалось прочитать файл «{_path}».", ex);
        }
    }

    public async Task SaveAsync(PosSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(PosFileMapper.Capture(snapshot), Options);
        try
        {
            await AtomicFile.WriteAsync(_path, data, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new PosFileException(FileError.StorageError, $"Не удалось сохранить файл «{_path}».", ex);
        }
    }
}
