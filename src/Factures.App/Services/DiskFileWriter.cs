using System.IO;
using Factures.Presentation.Services;

namespace Factures.App.Services;

public sealed class DiskFileWriter : IFileWriter
{
    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default) =>
        File.WriteAllBytesAsync(path, content, cancellationToken);
}
