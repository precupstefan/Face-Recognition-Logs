﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.Win32;

namespace Pontor
{
    /// <summary>
    /// Interaction logic for TrainingControl.xaml
    /// </summary>
    public partial class TrainingControl : UserControl
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public event EventHandler writeToConsole;

        List<Image<Gray, byte>> imagesToBeSaved = new List<Image<Gray, byte>>();
        List<Border> imagesToBeDeleted = new List<Border>();
        public bool isWaitingForImage = true;
        public String messageForConsole;
        public bool hasSaved = false;


        public TrainingControl()
        {
            InitializeComponent();
        }


        public void AddPictureToCollection(Image<Gray, byte> image)
        {
            Image<Gray, byte> img1 = new Image<Gray, byte>(image.ToBitmap());
            imagesToBeSaved.Add(img1);
            ImageSource img = ConvertToImageSource(image.Bitmap);
            previewImage.Source = img;
            Keep.IsEnabled = true;
            Discard.IsEnabled = true;
            isWaitingForImage = false;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Border border = (Border)sender;
            if (border.Background == null)
            {
                border.Background = System.Windows.Media.Brushes.CornflowerBlue;
                imagesToBeDeleted.Add(border);
            }
            else
            {
                border.Background = null;
                imagesToBeDeleted.Remove(border);
            }
            if(imagesToBeDeleted.Count==0)
            {
                removePicture.IsEnabled = false;
            }
            else
            {
                removePicture.IsEnabled = true;
            }
        }

        private ImageSource ConvertToImageSource(Bitmap bmp)
        {

            IntPtr hBitmap = bmp.GetHbitmap();
            ImageSource wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Dispose();
            DeleteObject(hBitmap);
            return wpfBitmap;

        }

        private void SaveDataSet_Click(object sender, RoutedEventArgs e)
        {
            String firstName = FirstNameTextBox.Text;
            String lastName = LastNameTextBox.Text;
            String CNP = CNPTextBox.Text;
            if (imagesToBeSaved.Count != MainWindow.capturesToBeTaken)
            {
                MessageBox.Show("Witchcraft!!! There should be " + MainWindow.capturesToBeTaken.ToString() + "" +
                    " pictures taken", "WIZZARD DETECTED", MessageBoxButton.OK, MessageBoxImage.Warning);
                MessageBox.Show(imagesToBeSaved.Count.ToString());
                return;
            }
            WriteToConsole("Training Mode : Saving " + imagesToBeSaved.Count + " images in folder pictures");
            if (SaveInDatabase(firstName, lastName, CNP))
            {

                int id = new SqlManager().SQL_GetPersonId(CNP);
                if (id == -1)
                {
                    MessageBox.Show("IMPOSIBLE! The ID is negative! Bring holy water!");
                    return;
                }
                SaveToFile(firstName, lastName, id);
            }
        }

        private void SaveToFile(string firstName, string lastName, int id)
        {
            int piccount = 0;
            var location = MainWindow.pathToSavePictures + "/";
            try
            {

                foreach (var image in imagesToBeSaved)
                {
                    SaveImage(image, id, piccount);
                    piccount++;

                }
                ResetEverything();
                imagesToBeSaved.Clear();

                WriteToConsole("Training Mode : Save succesful for " + firstName + " " + lastName);
                hasSaved = true;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }
        }

        private void ResetEverything()
        {

            CapturesDisplay.Children.Clear();
            imagesToBeDeleted.Clear();
            CapturesDisplay_ContentChanged();
            previewImage.Source = null;
            Keep.IsEnabled = false;
            Discard.IsEnabled = false;
            removePicture.IsEnabled = false;
            FirstNameTextBox.Text = "";
            LastNameTextBox.Text = "";
            CNPTextBox.Text = "";
            imagesToBeSaved.Clear();
        }

        private bool SaveInDatabase(string firstName, string lastName, string CNP)
        {
            if (CheckForEmptyFields(firstName, lastName, CNP))
            {
                try
                {
                    new SqlManager().SQL_InsertIntoPersons(firstName, lastName, CNP);
                    return true;
                }
                catch (IndexOutOfRangeException e)
                {
                    MessageBox.Show("There is 1 Person with same CNP. Please check your information or contact database administrator",
                     "CNP IN USE", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            return false;
        }

        private bool CheckForEmptyFields(string firstName, string lastName, string CNP)
        {
            if (String.IsNullOrEmpty(firstName))
            {
                MessageBox.Show("Field First Name can not be empty");
                FirstNameTextBox.Focus();
                return false;
            }
            else if (String.IsNullOrEmpty(lastName))
            {
                MessageBox.Show("Field First Name can not be empty");
                LastNameTextBox.Focus();
                return false;
            }
            else if (String.IsNullOrEmpty(CNP))
            {
                MessageBox.Show("Field CNP can not be empty");
                CNPTextBox.Focus();
                return false;
            }
            return true;
        }

        private void SaveImage(Image<Gray, byte> image, int id, int piccount)
        {
            Bitmap bmp = image.ToBitmap();
            String filePath = "pictures/" + id.ToString();
            filePath += "_" + piccount.ToString() + ".bmp";
            bmp.Save(filePath);
            bmp.Dispose();
        }

        private void removePicture_Click(object sender, RoutedEventArgs e)
        {
            if (imagesToBeDeleted.Count != 0)
            {
                foreach (var image in imagesToBeDeleted)
                {
                    int index=CapturesDisplay.Children.IndexOf(image);
                    imagesToBeSaved.RemoveAt(index);
                    CapturesDisplay.Children.Remove(image);
                }
                imagesToBeDeleted.Clear();
                MainWindow.capturesTaken = CapturesDisplay.Children.Count;
                CapturesDisplay_ContentChanged();
                removePicture.IsEnabled = false;
            }
        }

        private void Keep_Click(object sender, RoutedEventArgs e)
        {
            Border border = new Border() { Padding = new Thickness(5) };
            border.Child = new System.Windows.Controls.Image() { Source = previewImage.Source, Width = 95, Height = 95 };
            border.MouseLeftButtonDown += Border_MouseLeftButtonDown;
            CapturesDisplay.Children.Add(border);
            previewImage.Source = null;
            CapturesDisplay_ContentChanged();
            Keep.IsEnabled = false;
            isWaitingForImage = true;
            Discard.IsEnabled = false;
        }

        private void Discard_Click(object sender, RoutedEventArgs e)
        {
            previewImage.Source = null;
            Keep.IsEnabled = false;
            Discard.IsEnabled = false;
            isWaitingForImage = true;
        }

        private void CapturesDisplay_ContentChanged()
        {
            int count = CapturesDisplay.Children.Count;
            MainWindow.capturesTaken = count;
            capturesDisplaySizeLabel.Content = "You have taken " + count.ToString() + " pictures!";
            if (count == MainWindow.capturesToBeTaken)
            {
                capturesDisplaySizeLabel.Content += " You may save now";
                SaveDataSet.IsEnabled=true;
            }
            else
            {
                SaveDataSet.IsEnabled = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ResetEverything();
        }

        private void WriteToConsole(String msg)
        {
            messageForConsole = msg;
            if (writeToConsole != null)
                writeToConsole(this, EventArgs.Empty);
        }
    }
}
