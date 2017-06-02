#region Usings
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
#endregion

namespace Artemious.Helpers
{
    public static class FileIOWriterReplacementExtensions
    {
        public static async Task WriteTextAsync(this StorageFile file, string contents)
        {
            // Replacement for
            // await FileIO.WriteTextAsync(file, contents);

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var dataWriter = new DataWriter(outputStream))
                    {
                        dataWriter.WriteString(contents);
                        await dataWriter.StoreAsync();
                        await dataWriter.FlushAsync();
                    }
                }
            }
        }

        public static async Task WriteBytesAsync(this StorageFile file, byte[] buffer)
        {
            // Replacement for
            // await FileIO.WriteBytesAsync(file, buffer);

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var dataWriter = new DataWriter(outputStream))
                    {
                        dataWriter.WriteBytes(buffer);
                        await dataWriter.StoreAsync();
                        await dataWriter.FlushAsync();
                    }
                }
            }
        }
    }
}
