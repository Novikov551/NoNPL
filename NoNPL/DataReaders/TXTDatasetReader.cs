using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NoNPL.DataReaders;

public class TXTDatasetReader
{
    public async IAsyncEnumerable<List<ReadOnlyMemory<byte>>> ReadTXTDatasetAsync(
      string filePath,
      byte[] tokenBytes,
      int maxConcurrent = 32,
      long bufferSize = 100 * 1024 * 1024,
      int readBufferSize = 81920,
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                
            }
        }

        byte[] leftover = [];
        long fileSize = new FileInfo(filePath).Length;
        long totalBytesRead = 0;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, readBufferSize, true);

        while (totalBytesRead < fileSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] readBuffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(bufferSize, fileSize - totalBytesRead));
            try
            {
                int bytesRead = await fs.ReadAsync(readBuffer, 0, (int)Math.Min(bufferSize, fileSize - totalBytesRead), cancellationToken);
                if (bytesRead == 0) break;
                totalBytesRead += bytesRead;

                byte[] data = ArrayPool<byte>.Shared.Rent(bytesRead);
                Array.Copy(readBuffer, data, bytesRead);

                int combinedLength = leftover.Length + bytesRead;
                byte[] combined = ArrayPool<byte>.Shared.Rent(combinedLength);
                if (leftover.Length > 0)
                {
                    Array.Copy(leftover, 0, combined, 0, leftover.Length);
                }
                Array.Copy(data, 0, combined, leftover.Length, bytesRead);

                ArrayPool<byte>.Shared.Return(data);

                int lastTokenEnd = FindLastTokenEnd(combined, combinedLength, tokenBytes);
                if (lastTokenEnd == -1)
                {
                    lastTokenEnd = combinedLength;
                }

                byte[] chunkWithToken = ArrayPool<byte>.Shared.Rent(lastTokenEnd);
                Array.Copy(combined, 0, chunkWithToken, 0, lastTokenEnd);

                int leftoverLength = combinedLength - lastTokenEnd;
                byte[] newLeftover = new byte[leftoverLength];
                Array.Copy(combined, lastTokenEnd, newLeftover, 0, leftoverLength);
                leftover = newLeftover; 

                ArrayPool<byte>.Shared.Return(combined);

                var stories = SplitByToken(chunkWithToken, lastTokenEnd, tokenBytes);

                ArrayPool<byte>.Shared.Return(chunkWithToken);

                var batch = new List<ReadOnlyMemory<byte>>(maxConcurrent);
                foreach (var story in stories)
                {
                    batch.Add(story.AsMemory());
                    if (batch.Count >= maxConcurrent)
                    {
                        yield return batch;
                        batch = new List<ReadOnlyMemory<byte>>(maxConcurrent);
                    }
                }

                if (batch.Count > 0)
                    yield return batch;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(readBuffer);
            }
        }

        if (leftover.Length > 0)
        {
            var lastStories = SplitByToken(leftover, leftover.Length, tokenBytes);
            foreach (var story in lastStories)
            {
                yield return new List<ReadOnlyMemory<byte>> { story.AsMemory() };
            }
        }
    }

    private static int FindLastTokenEnd(byte[] data, int dataLength, byte[] token)
    {
        for (int i = dataLength - token.Length; i >= 0; i--)
        {
            bool found = true;
            for (int j = 0; j < token.Length; j++)
            {
                if (data[i + j] != token[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i + token.Length;
        }
        return -1;
    }

    private static IEnumerable<byte[]> SplitByToken(byte[] data, int dataLength, byte[] token)
    {
        int start = 0;
        while (start <= dataLength)
        {
            int tokenPos = FindFirstToken(data, dataLength, token, start);
            if (tokenPos == -1)
            {
                int length = dataLength - start;
                if (length > 0)
                {
                    byte[] part = new byte[length];
                    Array.Copy(data, start, part, 0, length);
                    yield return part;
                }
                break;
            }
            else
            {
                int partLength = tokenPos - start;
                if (partLength > 0)
                {
                    byte[] part = new byte[partLength];
                    Array.Copy(data, start, part, 0, partLength);
                    yield return part;
                }
                start = tokenPos + token.Length;
            }
        }
    }

    private static int FindFirstToken(byte[] data, int dataLength, byte[] token, int startIndex)
    {
        for (int i = startIndex; i <= dataLength - token.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < token.Length; j++)
            {
                if (data[i + j] != token[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }
}