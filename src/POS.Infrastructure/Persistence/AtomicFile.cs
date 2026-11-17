namespace POS.Infrastructure.Persistence;

internal static class AtomicFile
{
    public static async Task WriteAsync(string destination, byte[] data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(destination) ?? throw new IOException("Не указан каталог файла.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".pos-{Guid.NewGuid():N}.tmp");
        var ownsTemporary = false;
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 8192, useAsync: true))
            {
                ownsTemporary = true;
                await file.WriteAsync(data, cancellationToken);
                file.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            if (ownsTemporary)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
