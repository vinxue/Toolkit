using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Text;

namespace SecKit
{
    public static class Base64Converter
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static byte[] ParseInput(string input, bool hex)
        {
            return hex ? Convert.FromHexString(RemoveWhitespace(input)) : StrictUtf8.GetBytes(input);
        }

        public static string FormatOutput(byte[] bytes, bool hex)
        {
            return hex ? Convert.ToHexString(bytes) : StrictUtf8.GetString(bytes);
        }

        public static string Encode(byte[] bytes, bool urlSafe)
        {
            return urlSafe ? Base64Url.EncodeToString(bytes) : Convert.ToBase64String(bytes);
        }

        public static byte[] Decode(string input, bool urlSafe)
        {
            var normalized = RemoveWhitespace(input);
            foreach (char character in normalized)
            {
                if (!IsEncodedCharacter(character, urlSafe))
                    throw new FormatException("Input contains a character outside the selected Base64 alphabet.");
            }

            return urlSafe ? Base64Url.DecodeFromChars(normalized) : Convert.FromBase64String(normalized);
        }

        public static async Task ConvertFileAsync(string inputPath, string outputPath, bool encode, bool urlSafe,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            inputPath = Path.GetFullPath(inputPath);
            outputPath = Path.GetFullPath(outputPath);
            if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Input and output must be different files.");

            string temporaryPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $".seckit-{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var inputBuffer = new byte[65536 + 4];
                    var outputBuffer = new byte[Base64.GetMaxEncodedToUtf8Length(inputBuffer.Length)];
                    int buffered = 0;
                    long processed = 0;
                    while (true)
                    {
                        int read = await input.ReadAsync(inputBuffer.AsMemory(buffered, 65536), cancellationToken).ConfigureAwait(false);
                        bool finalBlock = read == 0;
                        processed += read;
                        int available = buffered + read;
                        if (!encode)
                            available = CompactEncodedBytes(inputBuffer, available, urlSafe);

                        int count = finalBlock ? available
                            : encode ? available - available % 3
                            : available == 0 ? 0 : (available - 1) / 4 * 4;
                        int written = TransformBlock(inputBuffer, count, outputBuffer, encode, urlSafe, finalBlock);
                        await output.WriteAsync(outputBuffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
                        buffered = available - count;
                        Buffer.BlockCopy(inputBuffer, count, inputBuffer, 0, buffered);
                        progress?.Report(input.Length == 0 ? 1 : (double)processed / input.Length);
                        if (finalBlock)
                            break;
                    }
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, outputPath, true);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static int CompactEncodedBytes(byte[] buffer, int length, bool urlSafe)
        {
            int written = 0;
            for (int index = 0; index < length; index++)
            {
                char character = (char)buffer[index];
                if (IsWhitespace(character))
                    continue;
                if (!IsEncodedCharacter(character, urlSafe))
                    throw new FormatException("Input contains a character outside the selected Base64 alphabet.");
                buffer[written++] = buffer[index];
            }
            return written;
        }

        private static int TransformBlock(byte[] input, int length, byte[] output, bool encode, bool urlSafe, bool finalBlock)
        {
            var source = input.AsSpan(0, length);
            if (!encode && !finalBlock && source.Contains((byte)'='))
                throw new FormatException("Base64 padding is only allowed at the end of the input.");

            int consumed;
            int written;
            OperationStatus status;
            if (encode)
            {
                status = urlSafe
                    ? Base64Url.EncodeToUtf8(source, output, out consumed, out written, finalBlock)
                    : Base64.EncodeToUtf8(source, output, out consumed, out written, finalBlock);
            }
            else
            {
                status = urlSafe
                    ? Base64Url.DecodeFromUtf8(source, output, out consumed, out written, finalBlock)
                    : Base64.DecodeFromUtf8(source, output, out consumed, out written, finalBlock);
            }
            if (status != OperationStatus.Done || consumed != length)
                throw new FormatException("Invalid Base64 length or padding for the selected format.");
            return written;
        }

        private static bool IsEncodedCharacter(char character, bool urlSafe)
        {
            return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '='
                || (urlSafe ? character is '-' or '_' : character is '+' or '/');
        }

        private static bool IsWhitespace(char character)
        {
            return character is ' ' or '\t' or '\r' or '\n';
        }

        private static string RemoveWhitespace(string input)
        {
            var result = new StringBuilder(input.Length);
            foreach (char character in input)
            {
                if (!IsWhitespace(character))
                    result.Append(character);
            }
            return result.ToString();
        }
    }
}
