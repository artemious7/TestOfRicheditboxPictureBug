#region Usings
using Artemious.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        readonly List<StringBuilder> picturesFromDocument = new List<StringBuilder>();
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
                args.mode = picturesFromDocument.Any() ? FixMode.RestorePictures : FixMode.ExtractPictures;
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
                        if (args.rtfString.StartsWith(@"{\pict"))
                        {
                            int rtfDocumentTagIndex = args.rtfString.IndexOf(@"{\rtf");
                            args.rtfString = args.rtfString.Substring(rtfDocumentTagIndex);
                            args.mode = FixMode.FixMessup;
                            fixRtf(args);
                            args.isBuggy_result = true;
                        }
                        else
                        {
                            // determine if the document is buggy. Won't change the 'rtfString'.
                            args.mode = FixMode.DetermineIfDocumentIsBuggy;
                            fixRtf(args);
                        }

                        if (args.isBuggy_result)
                        {
                            var args_extractPictures = new ArgumentsForFixRtf { mode = FixMode.ExtractPictures };
                            // generate clean RTF
                            args_extractPictures.rtfString = await cleanRtfGenerator();
                            // extract pictures from the clean RTF into 'picturesFromDocument' field.
                            fixRtf(args_extractPictures);

                            args.mode = FixMode.RestorePictures;
                            // fix the 'rtfString' using the pictures from 'picturesFromDocument'
                            fixRtf(args);
                        }
                    }
                }
                catch (Exception ex)
                {
                    //CommonExtensions.DebugBreak();
                    //EasyTracker.GetTracker().SendException(ex);
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
            fixRtf(ref args.rtfString, this.picturesFromDocument, ref args.isBuggy_result, args.mode);
        }

        static void fixRtf(ref string rtfString, List<StringBuilder> pictures, ref bool isBuggy_result, FixMode mode)
        {
            #region define variables

            #region mode
            bool extractPictures = false,
                 findPictures = false,
                 onlyDetermineIfDocumentIsBuggy = false,
                 restorePictures = false,
                 fixMessup = false;

            switch (mode)
            {
                case FixMode.ExtractPictures:
                    extractPictures = findPictures = true;
                    break;
                case FixMode.RestorePictures:
                    restorePictures = true;
                    break;
                case FixMode.DetermineIfDocumentIsBuggy:
                    onlyDetermineIfDocumentIsBuggy = true;
                    break;
                case FixMode.FixMessup:
                    fixMessup = findPictures = true;
                    break;
                default:
                    throw new NotImplementedException(mode.ToString());
            }
            #endregion

            bool metLastClosingBrace = false;
            const string PICTURE_RTF_TAG = @"{\pict"; // a picture start RTF tag
            int pictRtfIndex = 0; // index of a char in PICTURE_RTF_TAG which is currently being viewed. This is for searching the PICTURE_RTF_TAG in rtfString.
            StringBuilder currentPicture = null; // a picture that is currently being extracted.
            bool isPicture = false; // is it a picture that is currently being viewed.
            int openBracesCountOfPictureRtfTag = -1; // value of 'opening' when the PICTURE_RTF_TAG is met in rtfString. This is to determine the closing brace '}' in the picture.

            var indexesOfPicturesToRestore = new List<int>(pictures.Count); // if we are in 'restore pictures' mode, put their indexes within result array.

            int opening = 0; // count of currently open braces.
            int lastClosingBraceResultIndex = rtfString.Length; // last closing brace index within the result array. Define in with the highest value - last index of rtfString plus one - in order to know if (for some buggy reason) no braces were met during viewing the rtfString.
            var result = new StringBuilder(rtfString); // resulting string will be put here. Its length may be a couple characters smaller than rtfString.Length
            #endregion

            #region view all the rtfString characters

            for (int i = 0; i < result.Length; i++)
            {
                char symbol = result[i];
                switch (symbol)
                {
                    case '{':
                        // increment the current opening braces count.
                        opening++;

                        // if there are any braces after the last one, then it's a buggy document
                        if (onlyDetermineIfDocumentIsBuggy && metLastClosingBrace)
                        {
                            isBuggy_result = true;
                            return;
                        }
                        break;
                    case '}':
                        // decrement the current opening braces count.
                        opening--;

                        // if there are any braces after the last one, then it's a buggy document
                        if (onlyDetermineIfDocumentIsBuggy && metLastClosingBrace)
                        {
                            isBuggy_result = true;
                            return;
                        }

                        #region removing the odd closing brace
                        // if the last brace gets closed, don't put it to the 'result' array, because there are may be some other characters after this one.
                        // This is where we fix the bug by removing the odd closing brace after the picture block that is now missing.
                        // We will restore the last closing brace in the document, but later.
                        if (opening == 0)
                        {
                            opening++; // pretend that it is still open
                            // set the position, into which we will later restore the last closing brace in the document.
                            lastClosingBraceResultIndex = i;
                            if (onlyDetermineIfDocumentIsBuggy)
                                metLastClosingBrace = true;
                            else if (restorePictures)
                                indexesOfPicturesToRestore.Add(i);

                            result.Remove(i--, 1);
                            continue;
                        }
                        #endregion

                        #region if we are extracting pictures and this is the current picture closing brace

                        if (isPicture
                            && opening == openBracesCountOfPictureRtfTag - 1)
                        {
                            // finish this picture extraction
                            isPicture = false;
                            // in mode 'fixMessup' we'll leave the odd '}' brace as a flag of picture position - we'll not remove it from the 'result' string
                            if (!fixMessup)
                                // add the closing brace '}'
                                currentPicture.Append(symbol);
                            // free up some memory
                            currentPicture.Capacity = currentPicture.Length;
                            // remember the picture
                            pictures.Add(currentPicture);
                            if (fixMessup)
                                indexesOfPicturesToRestore.Add(i - currentPicture.Length);
                            pictRtfIndex = 0;
                        }
                        #endregion

                        break;
                }

                if (findPictures)
                {
                    if (isPicture)
                        currentPicture.Append(symbol);

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
                                // reserve the memory as if the picture was taking all the space in rtfString, because we do not know the exact size of the picture. We'll free up the memory later. (This is good only when a document contains 0, 1 or 2 pictures, but it is too much in other cases.)
                                currentPicture = new StringBuilder(PICTURE_RTF_TAG, rtfString.Length);
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
            }
            #endregion

            // from now on be careful with index values in result array, because we insert things into the result array, and it is not actual.

            #region restore the last closing brace
            // if there was a closing brace that we removed last
            if (lastClosingBraceResultIndex > 0)
                // insert the last closing brace
                result.Insert(lastClosingBraceResultIndex, '}');
            #endregion


            #region restoring or removing the pictures
            if (restorePictures || fixMessup)
            {
                if (restorePictures)
                    indexesOfPicturesToRestore.Remove(lastClosingBraceResultIndex);

                // insert pictures into (or remove from) 'result' - in reverse order by Index, because we are relying on the index (from 'indexesOfPicturesToRestore') of the collection that we change - 'result'.
                foreach (var p in pictures.Zip(indexesOfPicturesToRestore, (picture, index) => new PictureWithIndex(picture, index)).OrderByDescending(r => r.Index))
                {
                    if (fixMessup)
                        result.Remove(p.Index, p.Picture.Length);
                    else
                        result.Insert(p.Index, p.Picture.ToString());
                }
            }
            #endregion

            if (fixMessup)
                pictures.Clear();

            // return the result as string
            rtfString = result.ToString();
        }

        struct PictureWithIndex
        {
            public PictureWithIndex(StringBuilder picture, int index)
            {
                Picture = picture;
                Index = index;
            }

            public readonly StringBuilder Picture;
            public readonly int Index;
        }

        enum FixMode
        {
            ExtractPictures,
            RestorePictures,
            DetermineIfDocumentIsBuggy,
            FixMessup
        }

        class ArgumentsForFixRtf
        {
            public string rtfString;
            public bool isBuggy_result;
            public FixMode mode;
        }
    }
}
