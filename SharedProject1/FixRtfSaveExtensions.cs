#region Usings
using Artemious.Helpers;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#endregion

namespace Artemious.RichEditBoxNS
{
    public static class FixRtfSaveExtensions
    {
        #region RichEditBox extension methods

        public static async Task<StorageFile> SaveToFileAsync(this RichEditBox reb, StorageFile file)
        {
            var info = GetFixRtfSaveInfo(reb);
            return await info.SaveToFileAsync(file);
        }

        public static async Task LoadFromFileAsync(this RichEditBox reb, StorageFile file)
        {
            var info = GetFixRtfSaveInfo(reb);
            info.Clear();

            await info.LoadFromFileAsync(file);
        }

        public static async Task LoadFromStringAsync(this RichEditBox reb, string rtfString, Func<InMemoryRandomAccessStream, Task> takeOpenedStream = null)
        {
            var info = GetFixRtfSaveInfo(reb);
            info.Clear();

            // Initialize the in-memory stream where data will be stored.
            var stream = new InMemoryRandomAccessStream();
            try
            {
                // Create the data writer object backed by the in-memory stream.
                using (var dataWriter = new DataWriter(stream))
                {
                    // Parse the input stream and write each element separately.
                    dataWriter.WriteString(rtfString);

                    // Send the contents of the writer to the backing stream.
                    await dataWriter.StoreAsync();

                    // In order to prolong the lifetime of the stream, detach it from the 
                    // DataWriter so that it will not be closed when Dispose() is called on 
                    // dataWriter. Were we to fail to detach the stream, the call to 
                    // dataWriter.Dispose() would close the underlying stream, preventing 
                    // its subsequent use below.
                    dataWriter.DetachStream();
                }

                reb.Document.LoadFromStream(TextSetOptions.FormatRtf, stream);

                if (takeOpenedStream != null)
                    // responsibility for disposing the stream is on this function.
                    await takeOpenedStream(stream);
                else
                    stream.Dispose();
            }
            catch (Exception)
            {
                stream.Dispose();
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
                throw;
            }
        }
        #endregion


        #region RichEditBox.FixRtfSaveInfo attached proprety

        public static FixRtfSaveInfo GetFixRtfSaveInfo(DependencyObject obj)
        {
            var value = (FixRtfSaveInfo)obj.GetValue(FixRtfSaveInfoProperty);
            if (value == null)
            {
                var reb = (RichEditBox)obj;
                value = new FixRtfSaveInfo(reb);
                SetFixRtfSaveInfo(obj, value);
            }
            return value;
        }

        public static void SetFixRtfSaveInfo(DependencyObject obj, FixRtfSaveInfo value)
        {
            obj.SetValue(FixRtfSaveInfoProperty, value);
        }

        public static readonly DependencyProperty FixRtfSaveInfoProperty =
            DependencyProperty.RegisterAttached("FixRtfSaveInfo", typeof(FixRtfSaveInfo), typeof(FixRtfSaveExtensions), new PropertyMetadata(default(FixRtfSaveInfo), (d, args) => OnFixRtfSaveInfoChanged((RichEditBox)d, args)));

        private static void OnFixRtfSaveInfoChanged(RichEditBox reb, DependencyPropertyChangedEventArgs args)
        {
            var info = (FixRtfSaveInfo)args.NewValue;


        }
        #endregion
    }
}
