using Android.Content;
using Microsoft.Extensions.Logging;
using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Services;
using Remotely.Desktop.Shared.ViewModels;

namespace Remotely.Desktop.Android.Services;

/// <summary>
/// Handles file transfer to/from the Android device's external files directory.
/// Received files are saved under <c>Android/data/com.immense.remotely.desktop/files/Remotely/</c>.
/// </summary>
public class AndroidFileTransferService : IFileTransferService
{
    private static readonly SemaphoreSlim _writeLock = new(1, 1);
    private static readonly Dictionary<string, FileStream> _partialTransfers = new();

    private readonly Context _context;
    private readonly ILogger<AndroidFileTransferService> _logger;

    public AndroidFileTransferService(
        Context context,
        ILogger<AndroidFileTransferService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string GetBaseDirectory()
    {
        var dir = _context.GetExternalFilesDir("Remotely")?.AbsolutePath
            ?? Path.Combine(Path.GetTempPath(), "Remotely");

        Directory.CreateDirectory(dir);
        return dir;
    }

    public void OpenFileTransferWindow(IViewer viewer)
    {
        // Android does not have a windowed file transfer UI in this release.
        // A future iteration can open a notification or activity.
        _logger.LogInformation("File transfer window requested (not implemented for Android).");
    }

    public async Task ReceiveFile(
        byte[] buffer,
        string fileName,
        string messageId,
        bool endOfFile,
        bool startOfFile)
    {
        try
        {
            await _writeLock.WaitAsync();

            var baseDir = GetBaseDirectory();

            if (startOfFile)
            {
                var filePath = Path.Combine(baseDir, fileName);

                if (File.Exists(filePath))
                {
                    var count = 0;
                    var ext = Path.GetExtension(fileName);
                    var fileWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    while (File.Exists(filePath))
                    {
                        filePath = Path.Combine(baseDir, $"{fileWithoutExt}-{count}{ext}");
                        count++;
                    }
                }

                File.Create(filePath).Close();
                var fs = new FileStream(filePath, FileMode.OpenOrCreate);
                _partialTransfers[messageId] = fs;
            }

            if (_partialTransfers.TryGetValue(messageId, out var fileStream))
            {
                if (buffer?.Length > 0)
                {
                    await fileStream.WriteAsync(buffer);
                }

                if (endOfFile)
                {
                    fileStream.Close();
                    _partialTransfers.Remove(messageId);
                    _logger.LogInformation("File transfer complete. Saved to: {Path}", GetBaseDirectory());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while receiving file.");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task UploadFile(
        FileUpload fileUpload,
        IViewer viewer,
        Action<double> progressUpdateCallback,
        CancellationToken cancelToken)
    {
        try
        {
            await viewer.SendFile(fileUpload, progressUpdateCallback, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while uploading file.");
        }
    }
}
