using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class FileDownloader
{
    private static readonly HttpClient client = new HttpClient();
    private static CancellationTokenSource cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        Console.WriteLine("File Downloader Application");
        Console.WriteLine("----------------------------");

        try
        {
            Console.Write("Enter file URL to download: ");
            string url = Console.ReadLine();

            Console.Write("Enter destination path (including filename): ");
            string destinationPath = Console.ReadLine();

            Console.WriteLine("\nPress any key to start downloading...");
            Console.WriteLine("Press 'C' to cancel during download...");
            Console.ReadKey();

            // Start the download
            var downloadTask = DownloadFileWithProgressAsync(url, destinationPath, cts.Token);

            // Listen for cancellation
            var cancelTask = Task.Run(() =>
            {
                while (true)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.C)
                    {
                        cts.Cancel();
                        Console.WriteLine("\nDownload cancellation requested...");
                        break;
                    }
                    if (downloadTask.IsCompleted) break;
                    Thread.Sleep(100);
                }
            });

            await Task.WhenAny(downloadTask, cancelTask);

            if (downloadTask.IsCompleted && !downloadTask.IsFaulted)
            {
                Console.WriteLine("\nDownload completed successfully!");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nDownload was cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
        finally
        {
            cts.Dispose();
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task DownloadFileWithProgressAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            Console.WriteLine($"Total file size: {(totalBytes == -1 ? "unknown" : FormatFileSize(totalBytes))}");

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                var totalBytesRead = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                    totalBytesRead += bytesRead;

                    if (totalBytes != -1)
                    {
                        var progressPercentage = (double)totalBytesRead / totalBytes * 100;
                        Console.Write($"\rDownloaded: {FormatFileSize(totalBytesRead)} of {FormatFileSize(totalBytes)} ({progressPercentage:N2}%)");
                    }
                    else
                    {
                        Console.Write($"\rDownloaded: {FormatFileSize(totalBytesRead)}");
                    }
                }
                while (isMoreToRead);
            }
        }
    }

    static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}