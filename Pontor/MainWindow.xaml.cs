﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Interop;
using System.Windows.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Data.SQLite;
using AForge.Video.DirectShow;
using System.IO;
using System.Windows.Controls;
using System.Collections.Generic;
using Emgu.CV.Face;
using System.Threading;
using Emgu.CV.Cuda;
using Emgu.CV.UI;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pontor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 


    public partial class MainWindow : Window
    {

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);


        public static int capturesTaken = 0;
        public static int capturesToBeTaken = 20;
        public static String pathToSavePictures;

        bool imagesFound = false;
        Image<Gray, byte>[] trainingImages;
        int[] personID;
        EigenFaceRecognizer faceRecognizer = new EigenFaceRecognizer(90, 2500);

        private bool wasTrained = false;
        int sizeToBeSaved = 100;//size of the picture wich will be saved

        //WebCameraControl WebCam;
        Stopwatch sw = new Stopwatch();

        TrainingControl trainingControl = new TrainingControl();
        PredictControl predictControl;
        VideoCapture WebCam;

        bool isPersonInRange = false;
        bool detectFaces = true;
        bool isTraining = false;
        bool isGpuEnabled = false;
        bool isCudaEnabled = false;
        bool isRegistering = false;

        double scaleFactor;
        int minNeigbours;

        String appLocation;
        String cpuClassifierFileName;
        String cudaClassifierFileName;

        private CascadeClassifier cpuClassifier;
        CudaCascadeClassifier cudaClassifier;

        List<VideoCapture> videoCaptures = new List<VideoCapture>();

        public MainWindow()
        {
            InitializeComponent();
            PopulateStreamOptions();


            predictControl = new PredictControl(ConsoleOutput, ConsoleScrollBar);
            predictControl.MessageRecieved += new EventHandler(MessageRecieved);
            trainingControl.writeToConsole += new EventHandler(trainingControlWriteToConsole);


            var location = System.AppDomain.CurrentDomain.BaseDirectory;
            appLocation = location;
            cpuClassifierFileName = location + "/haarcascade_frontalface_alt_CPU.xml";
            cudaClassifierFileName = location + "/haarcascade_frontalface_alt_GPU.xml";
            CreateDirectory(location, "data");
            CreateDirectory(location, "pictures");
            CreateDirectory(location, "Logs-Pictures");


            SwitchToPredictMode();
            pathToSavePictures = location + "/pictures";
            SqlManager.SQL_CheckforDatabase();

            //loads model otherwise trains it
            if (!CheckForModel())
                LoadImages(location);


            CheckIfCudaIsEnabled();


            //test
            cpuClassifier = new CascadeClassifier(cpuClassifierFileName);
        }

        private void trainingControlWriteToConsole(object sender, EventArgs e)
        {
            String message = trainingControl.messageForConsole;
            if (message != null)
            {
                WriteToConsole(message);
                trainingControl.messageForConsole = null;
            }
        }


        //////private void tests(object sender,EventArgs arfs)
        //////{
        //////    if(WebCam!=null)
        //////    if (isCudaEnabled && isGpuEnabled)
        //////    {
        //////        Mat gm = new Mat();
        //////        gm = WebCam.QueryFrame();
        //////            if(gm!=null)
        //////        ProcessWithGPU(gm);
        //////    }
        //////    else
        //////    {
        //////        Mat m = new Mat();
        //////        m = WebCam.QueryFrame();
        //////            if(m!=null)
        //////        ProcessWithCPU(m);
        //////    }
        //////}

        private bool CheckForModel()
        {
            if (File.Exists(appLocation + "/data/faceRecognizerModel.cv"))
            {
                WriteToConsole("FaceRecognizer : Model found. Loaded and skiped training");
                faceRecognizer.Read(appLocation + "/data/faceRecognizerModel.cv");
                return true;
            }
            return false;
        }

        private void CheckIfCudaIsEnabled()
        {
            if (CudaInvoke.HasCuda)
            {
                isCudaEnabled = true;
                cudaClassifier = new CudaCascadeClassifier(cudaClassifierFileName);

            }
            else
            {
                WriteToConsole("Cuda is not enabled on this device");
                isCudaEnabled = false;
                hardwareSelector.IsEnabled = false;
            }
        }

        private void MessageRecieved(object sender, EventArgs e)
        {
            var message = predictControl.message;
            if (message == "R")
            {
                detectFaces = false;
            }
            else if (message == "Y")
            {
                detectFaces = true;
            }
        }

        #region FACERECOGNIZER
        public void TrainFaceRecognizer()
        {
            Thread t = new Thread(() =>
            {
                Thread.Sleep(500);
                WriteToConsole("FaceRecognizer : Training...");

                //Dispatcher.Invoke
                faceRecognizer.Train(trainingImages, personID);
                WriteToConsole("FaceRecognizer : Finished Training");
                isTraining = false;
                wasTrained = true;
            });
            t.Start();
            //faceRecognizer.Write("/data/ceva");
            //throw new NotImplementedException();
        }

        public void LoadImages(String location)
        {
            Thread t = new Thread(() =>
            {
                location += "/pictures";
                int count = Directory.GetFiles(location).Length;
                if (count > 0)
                {
                    WriteToConsole("FaceRecognizer : Found " + count.ToString() + " images.");
                    WriteToConsole("FaceRecognizer : Loading Images...");
                    isTraining = true;
                }
                trainingImages = new Image<Gray, byte>[count];
                personID = new int[count];
                int i = 0;
                foreach (string file in Directory.EnumerateFiles(location, "*.bmp"))
                {
                    trainingImages[i] = new Image<Gray, byte>(file);
                    string filename = Path.GetFileName(file);
                    var fileSplit = filename.Split('_');
                    int personid = Convert.ToInt32(fileSplit[0]);
                    personID[i] = personid;
                    i++;
                    imagesFound = true;
                }
                if (!imagesFound)
                {
                    MessageBox.Show("No pictures were found, please register a person", "Data not available", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Dispatcher.Invoke(() => { ModeSelector.IsChecked = true; });
                }
                if (imagesFound)
                {
                    WriteToConsole("FaceRecognizer : Images Loaded succesfully");
                    TrainFaceRecognizer();
                }
            });
            t.Start();

        }
        #endregion

        private void CreateDirectory(string location, string folder)
        {
            Directory.CreateDirectory(location + "/" + folder);
        }


        private void PopulateStreamOptions()
        {
            //get all connected webcams
            FilterInfoCollection x = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            int id = 0;
            foreach (FilterInfo info in x)
            {
                StreamingOptions.Items.Add(id);
                videoCaptures.Add(new VideoCapture(id));
                id++;
            }
            StreamingOptions.Items.Add("VIA IP");
        }

        private void WebCam_ImageGrabbed(object sender, EventArgs e)
        {
            try
            {
                Mat m = new Mat();

                WebCam.Retrieve(m);
                if (m != null)
                    if (isCudaEnabled && isGpuEnabled)
                    {

                        ProcessWithGPU(m);
                    }
                    else
                    {

                        ProcessWithCPU(m);
                    }
            }
            catch (System.AccessViolationException exx)
            { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (StreamingOptions.SelectedIndex == -1)
            {
                MessageBox.Show("Please select streaming Device");
                return;
            }
            string option = StreamingOptions.SelectedItem.ToString();
            string url = "http://";
            // url += UsernameStream.Text + ":";
            // url += PasswordStream.Text + "@";
            url += IP1.Text + ".";
            url += IP2.Text + ".";
            url += IP3.Text + ".";
            url += IP4.Text;
            url += ":8080/video";
            Thread t = new Thread(() =>
            {
                if (option == "VIA IP")
                {
                    WebCam = new VideoCapture(url);
                    WriteToConsole("Camera : Connected to external camera");
                }
                else
                {
                    // int id = Convert.ToInt32(StreamingOptions.SelectedItem);
                    WebCam = videoCaptures[Convert.ToInt32(option)];
                    WriteToConsole("Camera : Connected to internal camera");
                }
                WebCam.ImageGrabbed += WebCam_ImageGrabbed;
                //WebCam.SetCaptureProperty(CapProp.Buffersuze, 3);
                WebCam.Start();
            });
            t.Start();
            startCameraFeed.IsEnabled = false;
            stopCameraFeed.IsEnabled = true;
            imageDisplayBorder.Visibility = Visibility.Visible;
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WebCam != null)
                {
                    WriteToConsole("Camera : Camera stopped");
                    WebCam.ImageGrabbed -= WebCam_ImageGrabbed;
                    if (WebCam.IsOpened)
                        WebCam.Stop();
                    //if(StreamingOptions.SelectedValue.ToString()!="VIA IP")
                    //  WebCam.Dispose();
                }
                startCameraFeed.IsEnabled = true;
                stopCameraFeed.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }



        #region GPU PROCESSING

        private void ProcessWithGPU(Mat bmp)
        {
            ////sw.Start();
            using (Image<Bgr, byte> capturedImage = bmp.ToImage<Bgr, byte>())
            {
                using (CudaImage<Bgr, byte> cudaCapturedImage = new CudaImage<Bgr, byte>(bmp))
                {
                    using (CudaImage<Gray, byte> cudaGrayImage = cudaCapturedImage.Convert<Gray, byte>())
                    {
                        if ((predictControl.isArduinoEnabled == true && predictControl.isPersonInRange) || predictControl.isArduinoEnabled == false)
                        {
                            Rectangle[] faces = FindFacesUsingGPU(cudaGrayImage);
                            foreach (Rectangle face in faces)
                            {
                                using (var graycopy = capturedImage.Convert<Gray, byte>().Copy(face).Resize(sizeToBeSaved, sizeToBeSaved, Inter.Cubic))
                                {
                                    capturedImage.Draw(face, new Bgr(255, 0, 0), 3);  //draw a rectangle around the detected face
                                    if (isRegistering)
                                    {
                                        Dispatcher.Invoke(() => { AddPicturesToCollection(graycopy); });
                                    }
                                    else
                                    {
                                        int id = -1;
                                        var personName = PredictFace(graycopy, out id);
                                        predictControl.getMedianFaceRecognition(graycopy, id);
                                        //place name of the person on the image
                                        CvInvoke.PutText(capturedImage, personName, new System.Drawing.Point(face.X - 2, face.Y - 2), FontFace.HersheyComplex, 1, new Bgr(0, 255, 0).MCvScalar);
                                    }
                                }
                                //imageDisplay.Image = capturedImage;

                            }
                        }
                    }
                }
                Dispatcher.Invoke(() =>
                {
                    imageDisplay.Source = ConvertToImageSource(capturedImage.ToBitmap());
                });
            }
            ////sw.Stop();
            ////WriteToConsole("GPU   " + sw.Elapsed.Milliseconds.ToString());
        }

        private Rectangle[] FindFacesUsingGPU(CudaImage<Gray, byte> cudaCapturedImage)
        {
            //using (CudaCascadeClassifier face = new CudaCascadeClassifier(cudaClassifierFileName))
            using (GpuMat faceRegionMat = new GpuMat())
            {
                cudaClassifier.ScaleFactor = scaleFactor;
                cudaClassifier.MinNeighbors = minNeigbours;
                cudaClassifier.DetectMultiScale(cudaCapturedImage, faceRegionMat);
                Rectangle[] faceRegion = cudaClassifier.Convert(faceRegionMat);
                return faceRegion;
            }
        }

        #endregion
        #region CPU PROCESSING
        private void ProcessWithCPU(Mat bmp)
        {

            ////sw.Start();
            using (Image<Bgr, byte> capturedImage = new Image<Bgr, byte>(bmp.Bitmap))
            {
                using (Image<Gray, byte> grayCapturedImage = capturedImage.Convert<Gray, byte>())
                {
                    if ((predictControl.isArduinoEnabled == true && predictControl.isPersonInRange) || predictControl.isArduinoEnabled == false)
                    {
                        Rectangle[] faces = FindFacesUsingCPU(grayCapturedImage);
                        Image<Gray, byte> equalizedGrayCapturedImage = grayCapturedImage;
                        CvInvoke.EqualizeHist(grayCapturedImage, equalizedGrayCapturedImage);

                        foreach (Rectangle face in faces)
                        {
                            var grayCopy = equalizedGrayCapturedImage.Copy(face);

                            var mouths = FaceProcessing.DetectMouth(grayCopy);
                            foreach (Rectangle mouth in mouths)
                            {
                                Rectangle mth = new Rectangle(face.X + mouth.X, face.Y + face.Height / 2 + mouth.Y, mouth.Width, mouth.Height);
                                capturedImage.Draw(mth, new Bgr(0, 255, 0), 2);
                            }


                            var eyes = FaceProcessing.AlignFace(grayCopy, out double degreesToRotateFace);
                            foreach (var eye in eyes)
                            {
                                Rectangle rectangleEye = new Rectangle(face.X + eye.X, face.Y + eye.Y, eye.Width, eye.Height);
                                capturedImage.Draw(rectangleEye, new Bgr(0, 0, 255), 2);
                            }
                            Rectangle faceRect = face;
                            if (mouths != null && eyes != null && eyes.Length == 2 && mouths.Length == 1)
                                faceRect = FaceProcessing.GetABetterFace(eyes, mouths[0], face.X, face.Y);
                            capturedImage.Draw(faceRect, new Bgr(255, 0, 0), 3);  //draw a rectangle around the detected face



                            var rotatedGrayCapturedImage = equalizedGrayCapturedImage.Rotate(degreesToRotateFace, new Gray(220));
                            grayCopy = rotatedGrayCapturedImage.Copy(faceRect);
                            grayCopy = grayCopy.Resize(sizeToBeSaved, sizeToBeSaved, Inter.Cubic);


                            if (isRegistering)
                            {
                                Dispatcher.Invoke(() => { AddPicturesToCollection(grayCopy); });
                            }
                            else
                            {
                                int id = -1;
                                var personName = PredictFace(grayCopy, out id);
                                predictControl.getMedianFaceRecognition(grayCopy, id);
                                //place name of the person on the image
                                CvInvoke.PutText(capturedImage, personName, new System.Drawing.Point(face.X - 2, face.Y - 2), FontFace.HersheyComplex, 1, new Bgr(0, 255, 0).MCvScalar);
                            }
                        }
                    }
                    //imageDisplay.Image = capturedImage;
                    Dispatcher.Invoke(() =>
                    { imageDisplay.Source = ConvertToImageSource(capturedImage.ToBitmap()); });
                }
            }
            ////sw.Stop();
            ////long timp = sw.ElapsedMilliseconds;
            ////if (timp > 40)
            ////    WriteToConsole("CPU  " + timp.ToString());
            ////sw.Reset();
        }


        private Rectangle[] FindFacesUsingCPU(Image<Gray, byte> grayCapturedImage)
        {
            // using (CascadeClassifier face = new CascadeClassifier(cpuClassifierFileName))
            {
                var faces = cpuClassifier.DetectMultiScale(grayCapturedImage, scaleFactor, minNeigbours);
                return faces;
            }
        }
        #endregion

        private void AddPicturesToCollection(Image<Gray, byte> graycopy)
        {
            if (trainingControl.isWaitingForImage)
                if (capturesTaken < capturesToBeTaken)
                {
                    trainingControl.AddPictureToCollection(graycopy);
                }
        }

        private String PredictFace(Image<Gray, byte> image, out int id)
        {
            String personName;
            if (!wasTrained)
            {
                id = -1;
                personName = "UNKOWN";
            }
            else if (!isTraining)
            {
                var result = faceRecognizer.Predict(image);
                personName = SqlManager.SQL_GetPersonName(result.Label.ToString());
                id = result.Label;

            }
            else
            {
                personName = "IN-TRAINING";
                id = -1;
            }

            return personName;
        }


        //private void ProcessWithCPU(Mat bmp)
        //{
        //    Image<Bgr, byte> actualImage = new Image<Bgr, byte>(bmp.Bitmap);
        //    if (actualImage != null && detectFaces)
        //    {
        //        Image<Gray, byte> grayImage = actualImage.Convert<Gray, byte>();
        //        double scaleFactor = Convert.ToDouble(ScaleFactorValue.Text);
        //        int minNeigbours = Convert.ToInt32(MinNeigboursValue.Text);
        //        var faces = cascadeClassifier.DetectMultiScale(grayImage, scaleFactor, minNeigbours); //the actual face detection happens here
        //        foreach (var face in faces)
        //        {
        //            //get just the detected area(face)
        //            var graycopy = actualImage.Copy(face).Convert<Gray, byte>().Resize(sizeToBeSaved, sizeToBeSaved, Inter.Cubic);
        //            if (capturesTaken < capturesToBeTaken && ModeSelector.IsChecked == true)
        //            {
        //                trainingControl.AddPictureToCollection(graycopy);
        //                capturesTaken++;
        //            }
        //            //draw rectangle on detected face
        //            actualImage.Draw(face, new Bgr(255, 0, 0), 3); //the detected face(s) is highlighted here using a box that is drawn around it/them

        //            string personName = "UNKNOWN";
        //            if (imagesFound)
        //            {
        //                if (!isTraining)
        //                {
        //                    FaceRecognizer.PredictionResult result = faceRecognizer.Predict(graycopy);
        //                    personName = new SqlManager().SQL_GetPersonName(result.Label.ToString());
        //                }
        //                else
        //                {
        //                    personName = "In Training";
        //                }
        //            }
        //            //display name over detected face
        //            CvInvoke.PutText(actualImage, personName, new System.Drawing.Point(face.X - 2, face.Y - 2), FontFace.HersheyComplex, 1, new Bgr(0, 255, 0).MCvScalar);
        //        }
        //    }
        //    imageDisplay.Image = actualImage;
        //    // ImgViewer.Source = ConvertToImageSource(actualImage.ToBitmap());
        //}

        private ImageSource ConvertToImageSource(Bitmap bmp)
        {

            IntPtr hBitmap = bmp.GetHbitmap();
            ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Dispose();
            DeleteObject(hBitmap);
            return wpfBitmap;

        }

        private void ModeSelector_Checked(object sender, RoutedEventArgs e)
        {

            try
            {
                ModeSelector.Content = "Switch to Predict Mode";
                if (predictControl != null)
                    CustomControlContainer.Children.Remove(predictControl);
                SwitchToTrainingMode();
                predictControl.timerSave.Stop();
                predictControl.TimeToSaveFired();
            }
            catch (Exception ex)
            { MessageBox.Show(ex.ToString()); }
        }



        private void SwitchToPredictMode()
        {
            CustomControlContainer.Children.Add(predictControl);
            isRegistering = false;

        }

        private void SwitchToTrainingMode()
        {

            CustomControlContainer.Children.Add(trainingControl);
            isRegistering = true;

        }

        private void ModelSelector_Unchecked(object sender, RoutedEventArgs e)
        {

            try
            {
                ModeSelector.Content = "Switch to Training Mode!";
                if (trainingControl != null)
                    CustomControlContainer.Children.Remove(trainingControl);
                SwitchToPredictMode();
                if (trainingControl.hasSaved)
                {
                    LoadImages(System.AppDomain.CurrentDomain.BaseDirectory);
                }
                predictControl.timerSave.Start();
            }
            catch (Exception ex)
            { MessageBox.Show(ex.ToString()); }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isTraining)
            {
                MessageBoxResult result = MessageBox.Show("Face recognition in training. Do you want to quit?", "Possible data loss", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            if (capturesTaken > 0)
            {
                MessageBoxResult result = MessageBox.Show("You have unsaved pictures. Do you want to quit?", "Possible data loss", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            if (predictControl.serialPort != null && predictControl.serialPort.IsOpen)
            {
                predictControl.AppCloseBluetoothClose();
            }
            if (WebCam != null && WebCam.IsOpened)
            {
                // WebCam.ImageGrabbed -= WebCam_ImageGrabbed;
                WebCam.Stop();
                //WebCam.Dispose();
            }
            if (wasTrained)
            {
                WriteToConsole("Saving Model");
                faceRecognizer.Write(appLocation + "/data/faceRecognizerModel.cv");
            }
            predictControl.TimeToSaveFired();
            Environment.Exit(0);
        }

        private void WriteToConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ConsoleOutput.Text += DateTime.Now.ToString() + " @ ";
                ConsoleOutput.Text += message + "\n";
                ConsoleScrollBar.ScrollToBottom();
            });
        }

        private void hardwareSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (hardwareSelector.SelectedIndex == 0)
            {
                isGpuEnabled = false;
            }
            else
            {
                isGpuEnabled = true;
            }
        }

        private void ParameterChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                TextBox textBox = (TextBox)sender;
                if (textBox.Name == "ScaleFactorValue")
                {
                    scaleFactor = Convert.ToDouble(textBox.Text);
                }
                else
                    if (textBox.Name == "MinNeigboursValue")
                {
                    minNeigbours = Convert.ToInt32(textBox.Text);
                }
            }
            catch (Exception) { }
        }

        private void StreamingOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StreamingOptions.SelectedIndex == 0)
            {
                webcameraCredentials.IsEnabled = false;
            }
            else if (StreamingOptions.SelectedIndex == 1)
            {
                webcameraCredentials.IsEnabled = true;
            }
            if (!stopCameraFeed.IsEnabled)
            {
                startCameraFeed.IsEnabled = true;
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowAspectRatio.Register((Window)sender);
        }

        internal class WindowAspectRatio
        {
            private double _ratio;

            private WindowAspectRatio(Window window)
            {
                _ratio = window.Width / window.Height;
                ((HwndSource)HwndSource.FromVisual(window)).AddHook(DragHook);
            }

            public static void Register(Window window)
            {
                new WindowAspectRatio(window);
            }

            internal enum WM
            {
                WINDOWPOSCHANGING = 0x0046,
            }

            [Flags()]
            public enum SWP
            {
                NoMove = 0x2,
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct WINDOWPOS
            {
                public IntPtr hwnd;
                public IntPtr hwndInsertAfter;
                public int x;
                public int y;
                public int cx;
                public int cy;
                public int flags;
            }

            private IntPtr DragHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handeled)
            {
                if ((WM)msg == WM.WINDOWPOSCHANGING)
                {
                    WINDOWPOS position = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));

                    if ((position.flags & (int)SWP.NoMove) != 0 ||
                        HwndSource.FromHwnd(hwnd).RootVisual == null) return IntPtr.Zero;

                    position.cx = (int)(position.cy * _ratio);

                    Marshal.StructureToPtr(position, lParam, true);
                    handeled = true;
                }

                return IntPtr.Zero;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
