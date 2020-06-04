//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Runtime.InteropServices.WindowsRuntime;
//using Windows.Foundation;
//using Windows.Foundation.Collections;
//using Windows.UI.Xaml;
//using Windows.UI.Xaml.Controls;
//using Windows.UI.Xaml.Controls.Primitives;
//using Windows.UI.Xaml.Data;
//using Windows.UI.Xaml.Input;
//using Windows.UI.Xaml.Media;
//using Windows.UI.Xaml.Navigation;

using VisitorAnalyzer.Helpers;
using FrameSourceHelper_UWP;
using Microsoft.AI.Skills.SkillInterfacePreview;
using Microsoft.AI.Skills.Vision.ObjectDetectorPreview;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Media.FaceAnalysis;
using Microsoft.Toolkit.Uwp.Helpers;
using VisitorAnalyzer.Helpers.CustomVision;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VisitorAnalyzer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DateTime LastProcessed = DateTime.Now;
        private FaceDetector m_faceDetector = null;
        Helpers.CustomVision.MaskDetection MaskDetect { set; get; }
        private IFrameSource m_frameSource = null;
        private SpeechHelper speech;
        // Vision Skills
        private ObjectDetectorDescriptor m_descriptor = null;
        private ObjectDetectorBinding m_binding = null;
        private ObjectDetectorSkill m_skill = null;
        private IReadOnlyList<ISkillExecutionDevice> m_availableExecutionDevices = null;
        bool IsPlaying = false;
        bool IsProcessing;
        // Misc
        private BoundingBoxRenderer[] m_bboxRenderer = new BoundingBoxRenderer[DataConfig.CCTVCount];
        private HashSet<ObjectKind> m_objectKinds = null;

        // Frames
        private SoftwareBitmapSource[] m_processedBitmapSource = new SoftwareBitmapSource[DataConfig.CCTVCount];
        private Random Rnd = new Random();
        // Performance metrics
        private Stopwatch m_evalStopwatch = new Stopwatch();
        private float m_bindTime = 0;
        private float m_evalTime = 0;
        private Stopwatch m_renderStopwatch = new Stopwatch();
        private static List<string> Sounds = new List<string>();
        private static DateTime[] LastSaved = new DateTime[4];
        // Locks
        private SemaphoreSlim m_lock = new SemaphoreSlim(1);
        HttpClient client = new HttpClient();
        public MainPage()
        {
            this.InitializeComponent();
        }

        async void PlaySound(string SoundFile)
        {
            if (IsPlaying) return;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {


                IsPlaying = true;
                MediaElement mysong = speechMediaElement;// new MediaElement();
                Windows.Storage.StorageFolder folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
                Windows.Storage.StorageFile file = await folder.GetFileAsync(SoundFile);
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                mysong.SetSource(stream, file.ContentType);
                mysong.Play();

                //UI code here

            });
        }

        private void Mysong_MediaEnded(object sender, RoutedEventArgs e)
        {

            IsPlaying = false;
        }

        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);
                speechMediaElement.MediaEnded += Mysong_MediaEnded;
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                m_faceDetector = await FaceDetector.CreateAsync();
                MaskDetect = new Helpers.CustomVision.MaskDetection(new string[] { "mask", "no-mask" });
                // Load and create the model 
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/facemask.onnx"));
                await MaskDetect.Init(modelFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error: {ex.Message}");
                MaskDetect = null;

            }
            for (int i = 0; i < 4; i++)
            {
                LastSaved[i] = DateTime.MinValue;
            }
            if (Sounds.Count <= 0)
            {
                //Sounds.Add("wengi.mp3");
                //Sounds.Add("setan.wav");
                //Sounds.Add("setan2.wav");
                //Sounds.Add("zombie.wav");
                //Sounds.Add("zombie2.wav");
                //Sounds.Add("scream.mp3");
                //Sounds.Add("monster.mp3");

            }

            m_processedBitmapSource[0] = new SoftwareBitmapSource();
            CCTV1.Source = m_processedBitmapSource[0];

           
            // Initialize helper class used to render the skill results on screen
            m_bboxRenderer[0] = new BoundingBoxRenderer(UIOverlayCanvas1);
           

            m_lock.Wait();
            {
                NotifyUser("Initializing skill...");
                m_descriptor = new ObjectDetectorDescriptor();
                m_availableExecutionDevices = await m_descriptor.GetSupportedExecutionDevicesAsync();

                await InitializeObjectDetectorAsync();
                await UpdateSkillUIAsync();
            }
            m_lock.Release();

            // Ready to begin, enable buttons
            NotifyUser("Skill initialized. Select a media source from the top to begin.");
            //Loop();
           
            var availableFrameSourceGroups = await CameraHelper.GetFrameSourceGroupsAsync();
            if (availableFrameSourceGroups != null)
            {
                CameraHelper cameraHelper = new CameraHelper() { FrameSourceGroup = availableFrameSourceGroups.FirstOrDefault() };
                CameraPreviewControl.PreviewFailed += CameraPreviewControl_PreviewFailed;
                await CameraPreviewControl.StartAsync(cameraHelper);
                CameraPreviewControl.CameraHelper.FrameArrived += CameraPreviewControl_FrameArrived;
            }


        }
        private async void CameraPreviewControl_FrameArrived(object sender, FrameEventArgs e)
        {
            var videoFrame = e.VideoFrame;
            var softwareBitmap = videoFrame.SoftwareBitmap;
            if (!IsProcessing && (DateTime.Now - LastProcessed).Seconds > 2)
                await ProcessFrame(softwareBitmap);
        }

        private void CameraPreviewControl_PreviewFailed(object sender, PreviewFailedEventArgs e)
        {
            var errorMessage = e.Error;
        }
        /// <summary>
        /// Initialize the ObjectDetector skill
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private async Task InitializeObjectDetectorAsync(ISkillExecutionDevice device = null)
        {
            if (device != null)
            {
                m_skill = await m_descriptor.CreateSkillAsync(device) as ObjectDetectorSkill;
            }
            else
            {
                m_skill = await m_descriptor.CreateSkillAsync() as ObjectDetectorSkill;
            }
            m_binding = await m_skill.CreateSkillBindingAsync() as ObjectDetectorBinding;
        }
        /// <summary>
        /// Print a message to the UI
        /// </summary>
        /// <param name="message"></param>
        private void NotifyUser(String message)
        {
            if (Dispatcher.HasThreadAccess)
            {
                UIMessageTextBlock.Text = message;
            }
            else
            {
                Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UIMessageTextBlock.Text = message).AsTask().Wait();
            }
        }
        /// <summary>
        /// Populate UI with skill information and options
        /// </summary>
        /// <returns></returns>
        private async Task UpdateSkillUIAsync()
        {
            if (Dispatcher.HasThreadAccess)
            {
                // Show skill description members in UI
                UISkillName.Text = m_descriptor.Information.Name;

                UISkillDescription.Text = $"{m_descriptor.Information.Description}" +
                $"\n\tauthored by: {m_descriptor.Information.Author}" +
                $"\n\tpublished by: {m_descriptor.Information.Publisher}" +
                $"\n\tversion: {m_descriptor.Information.Version.Major}.{m_descriptor.Information.Version.Minor}" +
                $"\n\tunique ID: {m_descriptor.Information.Id}";

                var inputDesc = m_descriptor.InputFeatureDescriptors[0] as SkillFeatureImageDescriptor;
                UISkillInputDescription.Text = $"\tName: {inputDesc.Name}" +
                $"\n\tDescription: {inputDesc.Description}" +
                $"\n\tType: {inputDesc.FeatureKind}" +
                $"\n\tWidth: {inputDesc.Width}" +
                $"\n\tHeight: {inputDesc.Height}" +
                $"\n\tSupportedBitmapPixelFormat: {inputDesc.SupportedBitmapPixelFormat}" +
                $"\n\tSupportedBitmapAlphaMode: {inputDesc.SupportedBitmapAlphaMode}";

                var outputDesc = m_descriptor.OutputFeatureDescriptors[0] as ObjectDetectorResultListDescriptor;
                UISkillOutputDescription1.Text = $"\tName: {outputDesc.Name}, Description: {outputDesc.Description} \n\tType: Custom";

                if (m_availableExecutionDevices.Count == 0)
                {
                    NotifyUser("No execution devices available, this skill cannot run on this device");
                }
                else
                {

                    // Display available execution devices
                    UISkillExecutionDevices.ItemsSource = m_availableExecutionDevices.Select((device) => $"{device.ExecutionDeviceKind} | {device.Name}");

                    // Set SelectedIndex to index of currently selected device
                    for (int i = 0; i < m_availableExecutionDevices.Count; i++)
                    {
                        if (m_availableExecutionDevices[i].ExecutionDeviceKind == m_binding.Device.ExecutionDeviceKind
                            && m_availableExecutionDevices[i].Name == m_binding.Device.Name)
                        {
                            UISkillExecutionDevices.SelectedIndex = i;
                            break;
                        }
                    }

                }

                // Populate ObjectKind filters list with all possible classes supported by the detector
                // Exclude Undefined label (not used by the detector) from selector list
                UIObjectKindFilters.ItemsSource = Enum.GetValues(typeof(ObjectKind)).Cast<ObjectKind>().Where(kind => kind != ObjectKind.Undefined);
            }
            else
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => await UpdateSkillUIAsync());
            }
        }

        async Task ProcessFrame(SoftwareBitmap outputBitmap)
        {
            var index = 0;
            //for (int index = 0; index < DataConfig.CCTVCount; index++)
                try
                {
                    IsProcessing = true;
                    if (outputBitmap != null)
                    {
                        SoftwareBitmap displayableImage = SoftwareBitmap.Convert(outputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                        VideoFrame frame = VideoFrame.CreateWithSoftwareBitmap(displayableImage);
                        //do evaluation
                        await ExecuteFrame(frame, index);
                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
                finally
                {
                    LastProcessed = DateTime.Now;
                    IsProcessing = false;
                }

        }
        async Task Loop()
        {
            //int index = 0;
            Random rnd = new Random(Environment.TickCount);
            while (true)
            {
                //index = 0;
                for (int index = 0; index < DataConfig.CCTVCount; index++)
                //Parallel.For(0, 3, async(index) =>
                {
                    try
                    {
                        var itemUrl = DataConfig.CCTVURL[index];

                        var data = await client.GetByteArrayAsync(itemUrl + rnd.Next(100));
                        //BitmapImage bmp = new BitmapImage();
                        SoftwareBitmap outputBitmap = null;
                        using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                        {
                            await stream.WriteAsync(data.AsBuffer());
                            stream.Seek(0);
                            //await bmp.SetSourceAsync(stream);
                            //new
                            ImageEncodingProperties properties = ImageEncodingProperties.CreateJpeg();

                            var decoder = await BitmapDecoder.CreateAsync(stream);
                            outputBitmap = await decoder.GetSoftwareBitmapAsync();
                        }

                        if (outputBitmap != null)
                        {

                            //SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(data.AsBuffer(), BitmapPixelFormat.Bgra8, bmp.PixelWidth, bmp.PixelHeight, BitmapAlphaMode.Premultiplied);
                            SoftwareBitmap displayableImage = SoftwareBitmap.Convert(outputBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                            VideoFrame frame = VideoFrame.CreateWithSoftwareBitmap(displayableImage);
                            //do evaluation
                            await ExecuteFrame(frame, index);


                        }

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
                Thread.Sleep(DataConfig.EvalInterval);
            }

        }


        async Task ExecuteFrame(VideoFrame CurFrame, int CurIndex)
        {
            await m_lock.WaitAsync();
            try
            {

                await DetectObjectsAsync(CurFrame);
                await DisplayFrameAndResultAsync(CurFrame, CurIndex);
            }
            catch (Exception ex)
            {
                NotifyUser(ex.Message);
            }
            finally
            {
                m_lock.Release();
            }
        }

        /// <summary>
        /// Triggered when the expander is expanded and collapsed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UIExpander_Expanded(object sender, EventArgs e)
        {
            /*
            var expander = (sender as Expander);
            if (expander.IsExpanded)
            {
                UIVideoFeed.Visibility = Visibility.Collapsed;
            }
            else
            {
                UIVideoFeed.Visibility = Visibility.Visible;
            }*/
        }
        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UIObjectKindFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await m_lock.WaitAsync();
            {
                m_objectKinds = UIObjectKindFilters.SelectedItems.Cast<ObjectKind>().ToHashSet();
            }
            m_lock.Release();
        }
        /// <summary>
        /// Triggers when a skill execution device is selected from the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UISkillExecutionDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDevice = m_availableExecutionDevices[UISkillExecutionDevices.SelectedIndex];
            await m_lock.WaitAsync();
            {
                await InitializeObjectDetectorAsync(selectedDevice);
            }
            m_lock.Release();
            if (m_frameSource != null)
            {
                await m_frameSource.StartAsync();
            }
        }
        /// <summary>
        /// Bind and evaluate the frame with the ObjectDetector skill
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        private async Task DetectObjectsAsync(VideoFrame frame)
        {
            m_evalStopwatch.Restart();

            // Bind
            await m_binding.SetInputImageAsync(frame);

            m_bindTime = (float)m_evalStopwatch.ElapsedTicks / Stopwatch.Frequency * 1000f;
            m_evalStopwatch.Restart();

            // Evaluate
            await m_skill.EvaluateAsync(m_binding);

            m_evalTime = (float)m_evalStopwatch.ElapsedTicks / Stopwatch.Frequency * 1000f;
            m_evalStopwatch.Stop();
        }

        /// <summary>
        /// Render ObjectDetector skill results
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="objectDetections"></param>
        /// <returns></returns>
        private async Task DisplayFrameAndResultAsync(VideoFrame frame, int CCTVIndex)
        {

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {

                try
                {
                    SoftwareBitmap savedBmp = null;
                    if (frame.SoftwareBitmap != null)
                    {
                        await m_processedBitmapSource[CCTVIndex].SetBitmapAsync(frame.SoftwareBitmap);
                        savedBmp = frame.SoftwareBitmap;
                    }
                    else
                    {
                        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Direct3DSurface, BitmapAlphaMode.Ignore);
                        await m_processedBitmapSource[CCTVIndex].SetBitmapAsync(bitmap);
                        savedBmp = bitmap;
                    }

                    // Retrieve and filter results if requested
                    IReadOnlyList<ObjectDetectorResult> objectDetections = m_binding.DetectedObjects;
                    if (m_objectKinds?.Count > 0)
                    {
                        objectDetections = objectDetections.Where(det => m_objectKinds.Contains(det.Kind)).ToList();
                    }
                    if (objectDetections != null)
                    {
                        // Update displayed results
                        m_bboxRenderer[CCTVIndex].Render(objectDetections);
                        bool PersonDetected = false;
                        int PersonCount = 0;
                        var rects = new List<Rect>();
                        foreach (var obj in objectDetections)
                        {
                            if (obj.Kind.ToString().ToLower() == "person")
                            {
                                PersonCount++;
                                PersonDetected = true;
                                rects.Add(obj.Rect);
                            }
                        }
                        if (PersonDetected)
                        {
                            bool KeepDistance = false;
                            if ((bool)ChkSocialDistancing.IsChecked)
                            {
                                //make sure there is more than 1 person
                                if (rects.Count > 1)
                                {
                                    var res = SocialDistanceHelpers.Detect(rects.ToArray());
                                    if (res.Result)
                                    {
                                        KeepDistance = true;
                                        m_bboxRenderer[CCTVIndex].DistanceLineRender(res.Lines);
                                        await speech.Read($"Please keep distance in {DataConfig.RoomName[CCTVIndex]}");
                                    }
                                    else
                                    {
                                        m_bboxRenderer[CCTVIndex].ClearLineDistance();
                                    }

                                }
                                else
                                {
                                    m_bboxRenderer[CCTVIndex].ClearLineDistance();
                                }
                            }
                            else
                            {
                                m_bboxRenderer[CCTVIndex].ClearLineDistance();
                            }

                            if ((bool)ChkMode.IsChecked && Sounds.Count>0)
                                PlaySound(Sounds[Rnd.Next(0, Sounds.Count - 1)]);
                            else if (!KeepDistance && !(bool)ChkDetectMask.IsChecked)
                            {
                                await speech.Read($"I saw {PersonCount} person in {DataConfig.RoomName[CCTVIndex]}");
                            }
                                

                            bool IsFaceDetected = false;
                            if ((bool)ChkDetectMask.IsChecked)
                            {
                                SoftwareBitmap softwareBitmapInput = frame.SoftwareBitmap;
                                // Retrieve a SoftwareBitmap to run face detection
                                if (softwareBitmapInput == null)
                                {
                                    if (frame.Direct3DSurface == null)
                                    {
                                        throw (new ArgumentNullException("An invalid input frame has been bound"));
                                    }
                                    softwareBitmapInput = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Direct3DSurface);
                                }
                                // We need to convert the image into a format that's compatible with FaceDetector.
                                // Gray8 should be a good type but verify it against FaceDetector’s supported formats.
                                const BitmapPixelFormat InputPixelFormat = BitmapPixelFormat.Gray8;
                                if (FaceDetector.IsBitmapPixelFormatSupported(InputPixelFormat))
                                {
                                    using (var detectorInput = SoftwareBitmap.Convert(softwareBitmapInput, InputPixelFormat))
                                    {
                                        // Run face detection and retrieve face detection result
                                        var faceDetectionResult = await m_faceDetector.DetectFacesAsync(detectorInput);

                                        // If a face is found, update face rectangle feature
                                        if (faceDetectionResult.Count > 0)
                                        {
                                            IsFaceDetected = true;
                                            var ListFaces = new List<Rect>();
                                            foreach (var item in faceDetectionResult)
                                            {


                                                // Retrieve the face bound and enlarge it by a factor of 1.5x while also ensuring clamping to frame dimensions
                                                BitmapBounds faceBound = item.FaceBox;

                                                var additionalOffset = faceBound.Width / 2;
                                                var ax = (double) faceBound.X / detectorInput.PixelWidth; //Math.Max(0, faceBound.X - additionalOffset);
                                                var ay = (double)faceBound.Y / detectorInput.PixelHeight; //Math.Max(0, faceBound.Y - additionalOffset);
                                                var aWidth = (double)faceBound.Width / detectorInput.PixelWidth;  //(uint)Math.Min(faceBound.Width + 2 * additionalOffset, softwareBitmapInput.PixelWidth - faceBound.X);
                                                var aHeight = (double)faceBound.Height / detectorInput.PixelHeight;//(uint)Math.Min(faceBound.Height + 2 * additionalOffset, softwareBitmapInput.PixelHeight - faceBound.Y);
                                                ListFaces.Add(new Rect(ax,ay,aWidth,aHeight));
                                            }
                                            m_bboxRenderer[CCTVIndex].RenderFaceRect(ListFaces);
                                            var maskdetect = await MaskDetect.PredictImageAsync(frame);
                                            var noMaskCount = maskdetect.Where(x => x.TagName == "no-mask" && x.Probability>0.3).Count();
                                            var MaskCount = maskdetect.Where(x => x.TagName == "mask" && x.Probability > 0.3).Count();
                                            //var first = maskdetect.Where(x => x.Probability > 0.6).OrderByDescending(x=>x.Probability).FirstOrDefault();
                                            //m_bboxRenderer[CCTVIndex].RenderMaskLabel(new List<PredictionModel>() { first });
                                            if (noMaskCount > 0)
                                            {
                                                if (!KeepDistance)
                                                    await speech.Read($"please wear a face mask in {DataConfig.RoomName[CCTVIndex]}");
                                                PersonStatusLbl.Text = "Please wear a mask, you are not allowed to enter.";
                                            }else if(MaskCount>0)
                                            {
                                                await speech.Read($"good you are passed");
                                                PersonStatusLbl.Text = "You are wearing mask, good.";
                                            }

                                        }
                                        else
                                        {
                                            m_bboxRenderer[CCTVIndex].ClearFaceRect();
                                        }
                                    }
                                }
                            }
                            else
                            {
                                m_bboxRenderer[CCTVIndex].ClearFaceRect();
                            }
                            if (!IsFaceDetected)
                                m_bboxRenderer[CCTVIndex].ClearMaskLabel();
                            //save to picture libs
                            /*
                            String path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                            path += "\\CCTV";
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }*/
                            var TS = DateTime.Now - LastSaved[CCTVIndex];
                            if (savedBmp != null && TS.TotalSeconds > DataConfig.CaptureIntervalSecs && (bool)ChkMode.IsChecked)
                            {
                                var myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
                                Windows.Storage.StorageFolder storageFolder = myPictures.SaveFolder;

                                // Create sample file; replace if exists.
                                //Windows.Storage.StorageFolder storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(path);
                                Windows.Storage.StorageFile sampleFile =
                                    await storageFolder.CreateFileAsync($"cctv_{DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss")}_{CCTVIndex}.jpg",
                                        Windows.Storage.CreationCollisionOption.ReplaceExisting);
                                ImageHelpers.SaveSoftwareBitmapToFile(savedBmp, sampleFile);
                                LastSaved[CCTVIndex] = DateTime.Now;
                            }
                        }
                    }

                    // Update the displayed performance text
                    StatusLbl.Text = $"bind: {m_bindTime.ToString("F2")}ms, eval: {m_evalTime.ToString("F2")}ms";
                }
                catch (TaskCanceledException)
                {
                    // no-op: we expect this exception when we change media sources
                    // and can safely ignore/continue
                }
                catch (Exception ex)
                {
                    NotifyUser($"Exception while rendering results: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Triggered when the image control is resized, making sure the canvas size stays in sync with the frame display control.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UIProcessedPreview_SizeChanged1(object sender, SizeChangedEventArgs e)
        {
            // Make sure the aspect ratio is honored when rendering the body limbs
            //float cameraAspectRatio = (float)m_frameSource.FrameWidth / m_frameSource.FrameHeight;
            float previewAspectRatio = (float)(CCTV1.ActualWidth / CCTV1.ActualHeight);
            var cameraAspectRatio = previewAspectRatio;
            UIOverlayCanvas1.Width = cameraAspectRatio >= previewAspectRatio ? CCTV1.ActualWidth : CCTV1.ActualHeight * cameraAspectRatio;
            UIOverlayCanvas1.Height = cameraAspectRatio >= previewAspectRatio ? CCTV1.ActualWidth / cameraAspectRatio : CCTV1.ActualHeight;

            m_bboxRenderer[0].ResizeContent(e);
        }
       
    }
}
