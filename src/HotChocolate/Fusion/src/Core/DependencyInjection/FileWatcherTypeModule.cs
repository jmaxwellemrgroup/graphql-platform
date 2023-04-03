using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace Microsoft.Extensions.DependencyInjection;

internal sealed class FileWatcherTypeModule : ITypeModule, IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public event EventHandler<EventArgs>? TypesChanged;

    public FileWatcherTypeModule(string fileName)
    {
        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(fileName));
        if (directory is null)
        {
            throw new FileNotFoundException(
                "The file name must contain a directory path.",
                fileName);
        }

        _watcher = new FileSystemWatcher();
        _watcher.Path = directory;
        _watcher.Filter = fileName;

        _watcher.NotifyFilter =
            NotifyFilters.FileName |
            NotifyFilters.DirectoryName |
            NotifyFilters.Attributes |
            NotifyFilters.CreationTime |
            NotifyFilters.FileName |
            NotifyFilters.LastWrite |
            NotifyFilters.Size;

        // TODO : remove
        TypesChanged += (_, _) => Console.WriteLine("Types changed ...");

        _watcher.Created += (s, e) => TypesChanged?.Invoke(s, e);
        _watcher.Changed += (s, e) => TypesChanged?.Invoke(s, e);
        _watcher.EnableRaisingEvents = true;

        // TODO : remove
        Console.WriteLine("Listening ...");
    }

    public ValueTask<IReadOnlyCollection<ITypeSystemMember>> CreateTypesAsync(
        IDescriptorContext context,
        CancellationToken cancellationToken)
    {
        // TODO : remove
        Console.WriteLine("Rebuild schema ...");
        return new ValueTask<IReadOnlyCollection<ITypeSystemMember>>(
            Array.Empty<ITypeSystemMember>());
    }

    public void Dispose()
        => _watcher.Dispose();
}