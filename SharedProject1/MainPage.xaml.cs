#region Usings
using Artemious.RichEditBoxNS;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#endregion 

namespace TestOfRicheditboxPictureBug
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }

        void Page_Loaded(object sender, RoutedEventArgs e)
        {
            loadButton_Click(null, null);
        }

        const string OUTPUT_FILENAME = "outputFile.rtf";
        const string INPUT_FILENAME = "inputFile.rtf";
        Uri uri = new Uri(new Uri("ms-appx:///"), INPUT_FILENAME);
        static readonly StorageFolder outputFolder = KnownFolders.PicturesLibrary;

        private async Task<StorageFile> getInputFile()
        {
            return await StorageFile.GetFileFromApplicationUriAsync(uri);
        }

        async void loadButton_Click(object sender, RoutedEventArgs e)
        {
            var info = FixRtfSaveExtensions.GetFixRtfSaveInfo(reb);
            info.EnableFixing = false;

            StorageFile inputFile = await getInputFile();
            await reb.LoadFromFileAsync(inputFile);
            SavedTimes = 0;
        }

        async void saveWithFixingButton_Click(object sender, RoutedEventArgs e)
        {
            var info = FixRtfSaveExtensions.GetFixRtfSaveInfo(reb);
            info.EnableFixing = true;

            var file = await saveRebToFile();
            await outputFileSize(file);
            SavedTimes++;
        }

        private async Task<StorageFile> saveRebToFile()
        {
            var file = await outputFolder.CreateFileAsync(OUTPUT_FILENAME, CreationCollisionOption.ReplaceExisting);
            return await reb.SaveToFileAsync(file);
        }

        private async Task outputFileSize(StorageFile file)
        {
            var fileProps = await file.GetBasicPropertiesAsync();
            resultTb.Text = "Size of output file is: " + string.Format("{0:n0}", fileProps.Size) + " bytes";
        }

        async void reloadButton_Click(object sender, RoutedEventArgs e)
        {
            var info = FixRtfSaveExtensions.GetFixRtfSaveInfo(reb);
            info.EnableFixing = false;

            var file = await outputFolder.GetFileAsync(OUTPUT_FILENAME);
            await reb.LoadFromFileAsync(file);
            SavedTimes = 0;
        }

        async void saveWithBugButton_Click(object sender, RoutedEventArgs e)
        {
            var info = FixRtfSaveExtensions.GetFixRtfSaveInfo(reb);
            info.EnableFixing = false;

            var file = await saveRebToFile();
            await outputFileSize(file);
            SavedTimes++;
        }

        async void reloadWithFixingButton_Click(object sender, RoutedEventArgs e)
        {
            var info = FixRtfSaveExtensions.GetFixRtfSaveInfo(reb);
            info.EnableFixing = true;
            info.SetCleanRtfGenerator(cleanRtfGenerator);

            var file = await outputFolder.GetFileAsync(OUTPUT_FILENAME);
            await reb.LoadFromFileAsync(file);
            SavedTimes = 0;

            await outputFileSize(file);
        }

        async Task<string> cleanRtfGenerator()
        {
            StorageFile inputFile = await getInputFile();
            string cleanRtfString = await FileIO.ReadTextAsync(inputFile);
            return cleanRtfString;
        }

        public int SavedTimes
        {
            get { return (int)GetValue(SavedTimesProperty); }
            set { SetValue(SavedTimesProperty, value); }
        }

        public static readonly DependencyProperty SavedTimesProperty =
            DependencyProperty.Register("SavedTimes", typeof(int), typeof(MainPage), new PropertyMetadata(0));


    }
}
