namespace Acb.Ingestion;

public sealed class Ingestor
{
    public Task IngestAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
