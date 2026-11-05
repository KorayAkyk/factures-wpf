namespace Factures.Presentation.Services;

public interface IFileWriter
{
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default);
}
