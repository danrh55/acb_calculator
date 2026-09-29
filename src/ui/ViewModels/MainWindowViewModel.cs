using System.Threading.Tasks;
using Acb.Ingestion;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Acb.View.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Ingestor _ingestor = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    [NotifyCanExecuteChangedFor(nameof(IngestCommand))]
    private string? _selectedFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIngestStatus))]
    private string? _ingestStatus;

    public bool HasSelectedFile => SelectedFileName is not null;

    public bool HasIngestStatus => IngestStatus is not null;

    public IStorageFile? SelectedFile { get; private set; }

    [RelayCommand]
    private async Task SelectFileAsync(Window window)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a transaction export",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV file") { Patterns = new[] { "*.csv" } },
                FilePickerFileTypes.TextPlain,
            }
        });

        if (files is not { Count: > 0 })
        {
            return;
        }

        SelectedFile = files[0];
        SelectedFileName = files[0].Name;
    }

    [RelayCommand(CanExecute = nameof(CanIngest))]
    private async Task IngestAsync()
    {
        var path = SelectedFile?.Path.LocalPath;
        if (path is null)
        {
            IngestStatus = "No local path available for the selected file.";
            return;
        }

        await _ingestor.IngestAsync(path);

        IngestStatus = $"Ingest called for {SelectedFileName}";
    }

    private bool CanIngest() => SelectedFile is not null;
}
