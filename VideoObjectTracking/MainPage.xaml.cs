using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Media.Editing;
using Windows.Media.Core;
using Windows.Media.Transcoding;
using Windows.UI.Core;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VideoObjectTracking
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            composition = new MediaComposition();

        }
        private MediaComposition composition;

        private MediaStreamSource mediaStreamSource;

/*        public void UpdateMediaElementSource()
        {

            mediaStreamSource = composition.GeneratePreviewMediaStreamSource(
                (int)mediaPlayerElement.ActualWidth,
                (int)mediaPlayerElement.ActualHeight);

            mediaPlayerElement.Source = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);

        }*/

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
 //           mediaPlayerElement.Source = null;
            mediaStreamSource = null;
            base.OnNavigatedFrom(e);

        }

        private async Task RenderCompositionToFile()
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeChoices.Add("MP4 files", new List<string>() { ".mp4" });
            picker.SuggestedFileName = "RenderedComposition.mp4";

            Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                // Call RenderToFileAsync
                var saveOperation = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise);

                saveOperation.Progress = new AsyncOperationProgressHandler<TranscodeFailureReason, double>(async (info, progress) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        ShowErrorMessage(string.Format("Saving file... Progress: {0:F0}%", progress));
                    }));
                });
                saveOperation.Completed = new AsyncOperationWithProgressCompletedHandler<TranscodeFailureReason, double>(async (info, status) =>
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
                    {
                        try
                        {
                            var results = info.GetResults();
                            if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
                            {
                                ShowErrorMessage("Saving was unsuccessful");
                            }
                            else
                            {
                                ShowErrorMessage("Trimmed clip saved to file");
                            }
                        }
                        finally
                        {
                            // Update UI whether the operation succeeded or not
                        }

                    }));
                });
            }
            else
            {
                ShowErrorMessage("User cancelled the file selection");
            }
        }

        private async Task PickFileAndAddClip()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            Windows.Storage.StorageFile pickedFile = await picker.PickSingleFileAsync();
            if (pickedFile == null)
            {
                ShowErrorMessage("File picking cancelled");
                return;
            }

            // These files could be picked from a location that we won't have access to later
            var storageItemAccessList = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList;
            storageItemAccessList.Add(pickedFile);

            var clip = await MediaClip.CreateFromFileAsync(pickedFile);
            composition.Clips.Add(clip);

        }

        private void ShowErrorMessage(string v)
        {
            throw new NotImplementedException();
        }

        private async Task SaveComposition()
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeChoices.Add("Composition files", new List<string>() { ".cmp" });
            picker.SuggestedFileName = "SavedComposition";

            Windows.Storage.StorageFile compositionFile = await picker.PickSaveFileAsync();
            if (compositionFile == null)
            {
                ShowErrorMessage("User cancelled the file selection");
            }
            else
            {
                var action = composition.SaveAsync(compositionFile);
                action.Completed = (info, status) =>
                {
                    if (status != AsyncStatus.Completed)
                    {
                        ShowErrorMessage("Error saving composition");
                    }

                };
            }
        }

        private async Task OpenComposition()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".cmp");

            Windows.Storage.StorageFile compositionFile = await picker.PickSingleFileAsync();
            if (compositionFile == null)
            {
                ShowErrorMessage("File picking cancelled");
            }
            else
            {
                composition = null;
                composition = await MediaComposition.LoadAsync(compositionFile);

                if (composition != null)
                {
                    //UpdateMediaElementSource();

                }
                else
                {
                    ShowErrorMessage("Unable to open composition");
                }
            }
        }

        private async Task PickFileAndAddImage() {
            FileOpenPicker fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileOpenPicker.FileTypeFilter.Add(".jpg");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

            var inputFile = await fileOpenPicker.PickSingleFileAsync();

            if (inputFile == null)
            {
                // The user cancelled the picking operation
                return;
            }

            SoftwareBitmap inputBitmap;
            using (IRandomAccessStream stream = await inputFile.OpenAsync(FileAccessMode.Read))
            {
                // Create the decoder from the stream
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                // Get the SoftwareBitmap representation of the file
                inputBitmap = await decoder.GetSoftwareBitmapAsync();
            }

            if (inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8
                        || inputBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                inputBitmap = SoftwareBitmap.Convert(inputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            SoftwareBitmap outputBitmap = new SoftwareBitmap(inputBitmap.BitmapPixelFormat, inputBitmap.PixelWidth, inputBitmap.PixelHeight, BitmapAlphaMode.Premultiplied);


            var helper = new OpenCVBridge01.OpenCVHelper();
            helper.Blur(inputBitmap, outputBitmap);

            var bitmapSource = new SoftwareBitmapSource();
            await bitmapSource.SetBitmapAsync(outputBitmap);
            imageControl.Source = bitmapSource;

        }

        private async void btn_Click(object sender, RoutedEventArgs e)
        {
            await PickFileAndAddImage();
        }
    }
}
