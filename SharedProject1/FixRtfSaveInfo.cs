#region Usings
using Artemious.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
#endregion

namespace Artemious.RichEditBoxNS
{
    public class FixRtfSaveInfo
    {
        #region Constructor
        public FixRtfSaveInfo(RichEditBox reb)
        {
            if (reb == null)
                throw new ArgumentNullException(nameof(reb));
            this.reb = reb;
        }
        #endregion

        public static bool EnableFixing_global = CommonUIExtensions.IsWindows10;

        public bool EnableFixing { get; set; } = EnableFixing_global;


        #region Fields
        readonly RichEditBox reb;
        readonly List<List<char>> picturesFromDocument = new List<List<char>>();
        private Func<Task<string>> cleanRtfGenerator;
        #endregion

        public void SetCleanRtfGenerator(Func<Task<string>> cleanRtfGenerator)
        {
            if (cleanRtfGenerator == null) throw new ArgumentNullException(nameof(cleanRtfGenerator));
            this.cleanRtfGenerator = cleanRtfGenerator;
        }

        public async Task<StorageFile> SaveToFileAsync(StorageFile file)
        {
            #region define variables
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            var args = new ArgumentsForFixRtf();
            #endregion

            reb.Document.GetText(TextGetOptions.FormatRtf, out args.rtfString);

            if (EnableFixing)
            {
                args.extractPictures = !picturesFromDocument.Any();
                fixRtf(args);
            }

            await file.WriteTextAsync(args.rtfString);

            return file;
        }

        public async Task LoadFromFileAsync(StorageFile file)
        {
            var args = new ArgumentsForFixRtf();

            await Task.Run(async () =>
            {
                args.rtfString = await FileIO.ReadTextAsync(file);

                try
                {
                    if (EnableFixing && cleanRtfGenerator != null)
                    {
                        // determine if the document is buggy. Won't change the 'rtfString'.
                        args.onlyDetermineIfDocumentIsBuggy = true;
                        fixRtf(args);

                        if (args.isBuggy_result)
                        {
                            var args_extractPictures = new ArgumentsForFixRtf { extractPictures = true };
                            // generate clean RTF
                            args_extractPictures.rtfString = await cleanRtfGenerator();
                            // extract pictures from the clean RTF into 'picturesFromDocument' field.
                            fixRtf(args_extractPictures);

                            args.onlyDetermineIfDocumentIsBuggy = false;
                            // fix the 'rtfString' using the pictures from 'picturesFromDocument'
                            fixRtf(args);
                        }
                    }
                }
                catch (Exception)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                        System.Diagnostics.Debugger.Break();
                    args.isBuggy_result = false;
                }
            });

            // load the fixed RTF document
            reb.Document.SetText(TextSetOptions.FormatRtf, args.rtfString);

            if (args.isBuggy_result)
            {
                await file.WriteTextAsync(args.rtfString);
            }
        }

        public void Clear() => picturesFromDocument.Clear();

        void fixRtf(ArgumentsForFixRtf args)
        {
            fixRtf(ref args.rtfString, this.picturesFromDocument, args.extractPictures, args.onlyDetermineIfDocumentIsBuggy, ref args.isBuggy_result);
        }

        static void fixRtf(ref string rtfString, List<List<char>> pictures, bool extractPictures, bool onlyDetermineIfDocumentIsBuggy, ref bool isBuggy_result)
        {
            #region define variables

            //const int MAX_SYMBOLS_AFTER_LAST_CLOSING_BRACE = 6;
            bool metLastClosingBrace = false;
            const string PICTURE_RTF_TAG = @"{\pict"; // a picture start RTF tag
            int pictRtfIndex = 0; // index of a char in PICTURE_RTF_TAG which is currently being viewed. This is for searching the PICTURE_RTF_TAG in rtfString.
            List<char> currentPicture = null; // a picture that is currently being extracted. List of characters.
            bool isPicture = false; // is it a picture that is currently being viewed.
            int openBracesCountOfPictureRtfTag = -1; // value of 'opening' when the PICTURE_RTF_TAG is met in rtfString. This is to determine the closing brace '}' in the picture.
            int pictureToRestore_index = 0; // index of the picture that is being restored.

            bool restorePictures = !extractPictures; // is it restore pictures mode?
            int[] indexesOfPicturesToRestore = new int[pictures.Count + 1]; // if we are in 'restore pictures' mode, put their indexes within result array.

            int opening = 0; // count of currently open braces.
            int lastClosingBraceResultIndex = rtfString.Length; // last closing brace index within the result array. Define in with the highest value - last index of rtfString plus one - in order to know if (for some buggy reason) no braces were met during viewing the rtfString.
            char[] result = new char[rtfString.Length]; // resulting string will be put here. Its length may be a couple characters smaller than rtfString.Length
            int resultIndex = 0; // current index within the 'result' array 

            if (onlyDetermineIfDocumentIsBuggy)
                extractPictures = restorePictures = false;
            #endregion

            #region view all the rtfString characters

            for (int i = 0; i < rtfString.Length; i++)
            {
                char symbol = rtfString[i];

                // if there are any braces after the last one, then it's a buggy document
                if (onlyDetermineIfDocumentIsBuggy && metLastClosingBrace && (symbol == '}' || symbol == '{'))
                {
                    isBuggy_result = true;
                    return;
                }

                switch (symbol)
                {
                    case '{':
                        // increment the current opening braces count.
                        opening++;
                        break;
                    case '}':
                        // decrement the current opening braces count.
                        opening--;

                        #region removing the odd closing brace
                        // if the last brace gets closed, don't put it to the 'result' array, because there are may be some other characters after this one.
                        // This is where we fix the bug by removing the odd closing brace after the picture block that is now missing.
                        // We will restore the last closing brace in the document, but later.
                        if (opening == 0)
                        {
                            opening++; // pretend that it is still open
                            // set the position, into which we will later restore the last closing brace in the document.
                            lastClosingBraceResultIndex = resultIndex;
                            if (onlyDetermineIfDocumentIsBuggy)
                                metLastClosingBrace = true;
                            if (restorePictures)
                                indexesOfPicturesToRestore[pictureToRestore_index++] = resultIndex;
                            continue;
                        }
                        #endregion

                        #region if we are extracting pictures and this is the current picture closing brace

                        if (isPicture
                            && opening == openBracesCountOfPictureRtfTag - 1)
                        {
                            // finish this picture extraction
                            isPicture = false;
                            // add the closing brace '}'
                            currentPicture.Add(symbol);
                            // free up some memory
                            currentPicture.Capacity = currentPicture.Count;
                            // remember the picture
                            pictures.Add(currentPicture);
                            pictRtfIndex = 0;
                        }
                        #endregion

                        break;
                }

                if (extractPictures)
                {
                    if (isPicture)
                        currentPicture.Add(symbol);

                    #region searching the picture rtf starting tag
                    else
                    {
                        if (symbol == PICTURE_RTF_TAG[pictRtfIndex]
                          && pictRtfIndex < PICTURE_RTF_TAG.Length)
                        {
                            pictRtfIndex++;
                            // if we found the picture RTF starting tag
                            if (pictRtfIndex == PICTURE_RTF_TAG.Length)
                            {
                                // create the array for picture with the picture starting tag in it.
                                currentPicture = PICTURE_RTF_TAG.ToList();
                                currentPicture.Capacity = rtfString.Length;  // reserve the memory as if the picture was taking all the space in rtfString, because we do not know the exact size of the picture. We'll free up the memory later. (This is good only when a document contains 0, 1 or 2 pictures, but it is too much in other cases.)
                                isPicture = true;
                                openBracesCountOfPictureRtfTag = opening;
                            }
                        }
                        else
                        {
                            pictRtfIndex = 0;
                        }
                    }
                    #endregion
                }

                // put the symbol to the result and increment index in the resulting array
                result[resultIndex++] = symbol;
            }
            #endregion


            #region restore the last closing brace
            // if there was a closing brace that we removed last
            if (lastClosingBraceResultIndex > 0)
            {
                // Shift the last characters in result array by 1 position forward - so that we could insert the last closing brace.
                for (int i = resultIndex - 1; i > lastClosingBraceResultIndex; i--)
                    result[i] = result[i - 1];
                // insert the last closing brace
                result[lastClosingBraceResultIndex] = '}';
            }
            #endregion


            if (restorePictures)
            {
                #region restoring the pictures
                var resultWithPictures = result.ToList();
                // free up memory
                result = null;

                // insert pictures into the 'resultWithPictures' list - in reverse order, because we are relying on the index (from 'indexesOfPicturesToRestore') of the collection that we change - 'resultWithPictures'.
                for (int i = pictures.Count - 1; i >= 0; i--)
                {
                    var picture = pictures[i];
                    int index = indexesOfPicturesToRestore[i];
                    resultWithPictures.InsertRange(index, picture);
                }

                rtfString = new string(resultWithPictures.ToArray());
                #endregion
            }
            else
                // return the result as string
                rtfString = new string(result);
        }

        enum FixMode
        {
            ExtractPictures,
            RestorePictures,
            OnlyDetermineIfDocumentIsBuggy
        }

        class ArgumentsForFixRtf
        {
            public string rtfString;
            public bool isBuggy_result;
            public bool extractPictures;
            public bool onlyDetermineIfDocumentIsBuggy;
        }
    }
}
