using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static SylverInk.CommonUtils;

namespace SylverInk.Net;

public static class HttpClientUtils
{
	/// <summary>
	/// This method encapsulates the process of asynchronously downloading a file from the internet and saving it to a local path.
	/// </summary>
	/// <param name="client">An existing HttpClient object.</param>
	/// <param name="uri">The URI of the file to download.</param>
	/// <param name="FileName">The local file path to save the downloaded file.</param>
	/// <returns>An awaitable <c>Task</c> representing the download operation.</returns>
	public static async Task DownloadFileTaskAsync(this HttpClient client, string uri, string FileName, CancellationTokenSource tokenSource)
	{
		using var fs = new FileStream(FileName, FileMode.Create);
		using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

		using var download = await response.Content.ReadAsStreamAsync(tokenSource.Token);
		await CopyToAsync(download, fs, response.Content.Headers.ContentLength ?? 0, tokenSource.Token);
	}

	private static async Task CopyToAsync(Stream source, Stream destination, long totalSize, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(destination);
		ArgumentOutOfRangeException.ThrowIfNegative(totalSize);

		if (!destination.CanWrite)
			return;
		if (!source.CanRead)
			return;

		var buffer = new byte[81920];
		long totalBytesRead = 0;
		int bytesRead;

		while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
		{
			await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
			totalBytesRead += bytesRead;
			Concurrent(() => UpdateHandler.UpdateWindow?.ReportProgress((double)totalBytesRead / totalSize));
		}
	}
}
