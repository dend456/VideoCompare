using System;
using System.Collections.Generic;
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
using System.IO;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.WPF;
using Microsoft.Win32;

namespace VidCompare
{
    
    public partial class MainWindow : Window
    {
        int LARGE_JUMP_STEP = 30;
        int SMALL_JUMP_STEP = 1;

        string[] IMAGE_FORMATS = { ".bmp", ".dib", ".jpeg", ".jpg", ".jpe", ".jp2",
            ".png", ".pbm", ".pgm", ".ppm", ".sr", ".ras", ".tiff", ".tif" };

        Mat[] mats = new Mat[3];
        Mat[] matsOrig = new Mat[3];
        Image[] images;
        TextBox[] frameBoxes;
        Slider[] brightnessSliders;
        Slider[] contrastSliders;
        Label[] brightnessLabels;
        Label[] contrastLabels;
        CheckBox[] invertedBoxes;
        CheckBox[] grayscaleBoxes;
        Label[] timeLabels;
        Capture[] videos = new Capture[2];
        VideoProperties[] properties = new VideoProperties[4];

        public MainWindow()
        {
            InitializeComponent();

            images = new Image[3] { image0, image1, image2 };
            frameBoxes = new TextBox[3] { frameBox0, frameBox1, frameBox2};
            brightnessSliders = new Slider[4] { brightnessSlider0, brightnessSlider1, brightnessSlider2, brightnessSlider3 };
            contrastSliders = new Slider[4] { contrastSlider0, contrastSlider1, contrastSlider2, contrastSlider3 };
            brightnessLabels = new Label[4] { brightnessLabel0, brightnessLabel1, brightnessLabel2, brightnessLabel3 };
            contrastLabels = new Label[4] { contrastLabel0, contrastLabel1, contrastLabel2, contrastLabel3 };
            invertedBoxes = new CheckBox[4] { invertBox0, invertBox1, invertBox2, invertBox3 };
            grayscaleBoxes = new CheckBox[4] { grayscaleBox0, grayscaleBox1, grayscaleBox2, grayscaleBox3 };
            timeLabels = new Label[2] { timeLabel0, timeLabel1 };

            for (int i = 0; i < properties.Length; ++i)
            {
                properties[i] = new VideoProperties();
            }
        }

        private T clamp<T>(T value, T min, T max) where T : System.IComparable<T>
        {
            T result = value;
            if (value.CompareTo(max) > 0)
            {
                result = max;
            }
            if (value.CompareTo(min) < 0)
            {
                result = min;
            }
            return result;
        }

        private void setGrayscale(int min, int max)
        {
            Parallel.For(min, max, (i) =>
            //for (int i = 0; i < matsOrig.Length; ++i)
            {
                if (matsOrig[i] != null)
                {
                    if ((properties[i].grayscale || properties[3].grayscale) && matsOrig[i].NumberOfChannels > 1)
                    {
                        CvInvoke.CvtColor(matsOrig[i], mats[i], ColorConversion.Bgr2Gray);
                    }
                    else
                    {
                        mats[i] = matsOrig[i].Clone();
                    }
                }
            });
        }

        private void setLevels(int min, int max)
        {
            if (brightnessSliders == null)
            {
                return;
            }

            Parallel.For(min, max, (i) => 
            //for (int i = 0; i < matsOrig.Length - 1; ++i)
            {
                if (matsOrig[i] != null)
                {
                    var brightnessSlider = brightnessSliders[i];
                    var contrastSlider = contrastSliders[i];
                    int brightness = (properties[3].brightness == 0) ? properties[i].brightness : properties[3].brightness;
                    brightness = (int)((brightness / 100.0f) * 255);
                    float contrast = (properties[3].contrast == 0) ? properties[i].contrast : properties[3].contrast;
                    contrast = (contrast + 100) / 100.0f;
                    mats[i].ConvertTo(mats[i], mats[i].Depth, contrast, brightness);
                }
            });
        }

        private void invert(int min, int max)
        {
            Parallel.For(min, max, (i) =>
            {
                if (matsOrig[i] != null && (properties[i].inverted || properties[3].inverted))
                {
                    CvInvoke.BitwiseNot(mats[i], mats[i]);
                }
            });
        }

        private void applyProperties()
        {
            setGrayscale(0, 2);
            invert(0, 2);
            setLevels(0, 2);

            for (int i = 0; i < mats.Length - 1; ++i)
            {
                if (mats[i] != null)
                {
                    images[i].Source = BitmapSourceConvert.ToBitmapSource(mats[i]);
                }
            }

            if (image0.Source != null && image1.Source != null)
            {
                float scale = 1.0f;
                bool res = float.TryParse(scaleBox.Text, out scale);
                matsOrig[2] = new Mat();
                CvInvoke.AbsDiff(mats[0], mats[1], matsOrig[2]);
                mats[2] = matsOrig[2].Clone();
                setGrayscale(2, 3);
                invert(2, 3);
                setLevels(2, 3);
                if (res)
                {
                    mats[2].ConvertTo(mats[2], mats[2].Depth, scale);
                }
                images[2].Source = BitmapSourceConvert.ToBitmapSource(mats[2]);
            }
        }

        private void loadImage(int input, string file)
        {
            matsOrig[input] = CvInvoke.Imread(file, LoadImageType.Unchanged);
            if (matsOrig[input].NumberOfChannels == 4)
            {
                CvInvoke.CvtColor(matsOrig[input], matsOrig[input], ColorConversion.Bgra2Bgr);
            }

            mats[input] = matsOrig[input].Clone();
            properties[input].format = InputFormat.Image;
            changeFrame(input, 0);
        }

        private void loadVideo(int input, string file)
        {
            videos[input] = new Capture(file);
            videos[input].Grab();
            matsOrig[input] = videos[input].QueryFrame();
            mats[input] = matsOrig[input].Clone();
            properties[input].format = InputFormat.Video;
            changeFrame(input, 0);
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Multiselect = false;

            var res = dialog.ShowDialog(this);
            if (res.HasValue && res.Value)
            {
                var file = dialog.FileName;
                var ext = System.IO.Path.GetExtension(file);
                int input = (sender == loadButton0) ? 0 : 1;
                if (IMAGE_FORMATS.Contains(ext))
                {
                    try
                    {
                        loadImage(input, file);
                    }
                    catch
                    {
                        MessageBox.Show("Error loading image");
                    }
                }
                else
                {
                    try
                    {
                        loadVideo(input, file);
                    }
                    catch
                    {
                        MessageBox.Show("Error loading video");
                    }
                }

                applyProperties();
            }
        }

        private int changeFrame(int input, int frame)
        {
            if (input < 2 && images[input] != null)
            {
                if (properties[input].format == InputFormat.Image)
                {
                    frame = properties[input].frame;
                    timeLabels[input].Content = "--:--/--:--";
                }
                else
                {
                    int max = (int)videos[input].GetCaptureProperty(CapProp.FrameCount);
                    if (frame == -1)
                    {
                        frame = max - 1;
                    }
                    else
                    {
                        frame = Math.Max(0, frame);
                        frame = Math.Min(max - 1, frame);
                    }
                    videos[input].SetCaptureProperty(CapProp.PosFrames, frame);
                    double fps = videos[input].GetCaptureProperty(CapProp.Fps);

                    try
                    {
                        videos[input].Grab();
                        matsOrig[input] = videos[input].QueryFrame();
                        mats[input] = matsOrig[input].Clone();
                    }
                    catch
                    {
                        matsOrig[input] = new Mat(new System.Drawing.Size(mats[input].Size.Width, mats[input].Size.Height), mats[input].Depth, mats[input].NumberOfChannels);

                        matsOrig[input].SetTo(new Bgr(255, 255, 255).MCvScalar);
                        CvInvoke.PutText(matsOrig[input], "Error: Unable to get frame.", new System.Drawing.Point(25, 250),
                            FontFace.HersheyPlain, 3.0, new Bgr(0, 0, 0).MCvScalar);
                        mats[input] = matsOrig[input].Clone();
                    }
                    int totalSeconds = (int)(max / fps);
                    int totalMinutes = totalSeconds / 60;
                    totalSeconds %= 60;
                    int seconds = (int)(frame / fps);
                    int minutes = seconds / 60;
                    seconds %= 60;
                    timeLabels[input].Content = String.Format("{0:D2}:{1:D2}/{2:D2}:{3:D2}", minutes, seconds, totalMinutes, totalSeconds);
                }
            }
            else if (input == 2)
            {
                if (matsOrig[0] != null)
                {
                    frame = int.Parse(frameBox0.Text);
                }
                else if (matsOrig[1] != null)
                {
                    frame = int.Parse(frameBox1.Text);
                }
                else
                {
                    frame = 0;
                }
            }
            frameBoxes[input].Text = frame.ToString();
            return frame;
        }

        private void frameButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int input = button.Name[button.Name.Length - 1] - '0';
            int frames = (button.Name.StartsWith("large")) ? LARGE_JUMP_STEP : SMALL_JUMP_STEP;
            int forwards = (button.Name.Contains("Forwards")) ? 1 : -1;

            if (input == 2)
            {
                for (int i = 0; i < 3; ++i)
                {
                    int current = int.Parse(frameBoxes[i].Text);
                    current += frames * forwards;
                    current = changeFrame(i, current);
                }
            }
            else
            {
                int current = int.Parse(frameBoxes[input].Text);
                current += frames * forwards;
                current = changeFrame(input, current);
            }
            applyProperties();
        }

        private void grayscaleBox_Checked(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            int input = box.Name[box.Name.Length - 1] - '0';

            if (properties != null)
            {
                properties[input].grayscale = true;
                if (input < 2)
                {
                    int other = (input == 0) ? 1 : 0;
                    properties[other].grayscale = true;
                    grayscaleBoxes[other].IsChecked = true;
                }
                applyProperties();
            }
        }

        private void grayscaleBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            int input = box.Name[box.Name.Length - 1] - '0';

            if (properties != null)
            {
                properties[input].grayscale = false;
                if (input < 2)
                {
                    int other = (input == 0) ? 1 : 0;
                    properties[other].grayscale = false;
                    grayscaleBoxes[other].IsChecked = false;
                }
                applyProperties();
            }
        }

        private void clearButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int input = button.Name[button.Name.Length - 1] - '0';
            bool uncheckGrayscale = false;
            if (input < 2 && properties[input].grayscale)
            {
                uncheckGrayscale = true;
            }

            properties[input].clear();
            brightnessSliders[input].Value = properties[input].brightness;
            contrastSliders[input].Value = properties[input].contrast;
            invertedBoxes[input].IsChecked = properties[input].inverted;
            grayscaleBoxes[input].IsChecked = properties[input].grayscale;
            
            if (uncheckGrayscale)
            {
                int other = (input == 0) ? 1 : 0;
                properties[other].grayscale = false;
                grayscaleBoxes[other].IsChecked = false;
            }

            applyProperties();
        }

        private void frameTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            var box = sender as TextBox;
            if (e.Key == Key.Enter)
            {
                int input = box.Name[box.Name.Length - 1] - '0';
                int frame = 0;
                bool res = int.TryParse(box.Text, out frame);
                if (res)
                {
                    if (input == 2)
                    {
                        for (int i = 0; i < 3; ++i)
                        {
                            frame = changeFrame(i, frame);
                        }
                    }
                    else
                    {
                        frame = changeFrame(input, frame);
                    }
                }
                else
                {
                    if (input == 2)
                    {
                        for (int i = 0; i < 3; ++i)
                        {
                            frame = changeFrame(i, 0);
                        }
                    }
                    else
                    {
                        frame = changeFrame(input, 0);
                    }
                }
                applyProperties();
                box.Text = frame.ToString();
                box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void brightnessSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (properties[0] == null)
            {
                return;
            }
            Slider slider = sender as Slider;
            int input = slider.Name[slider.Name.Length - 1] - '0';
            properties[input].brightness = (int)(slider.Value);
            brightnessLabels[input].Content = slider.Value.ToString();
            applyProperties();
        }

        private void contrastSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (properties[0] == null)
            {
                return;
            }
            Slider slider = sender as Slider;
            int input = slider.Name[slider.Name.Length - 1] - '0';
            properties[input].contrast = (int)(slider.Value);
            contrastLabels[input].Content = slider.Value.ToString();
            applyProperties();
        }

        private void scaleBox_KeyDown(object sender, KeyEventArgs e)
        {
            var box = sender as TextBox;
            if (e.Key == Key.Enter)
            {
                applyProperties();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                frameButton_Click(smallBackwards2, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                frameButton_Click(smallForwards2, null);
                e.Handled = true;
            }
        }
        
        private void endButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int input = button.Name[button.Name.Length - 1] - '0';

            if (input == 2)
            {
                for (int i = 0; i < images.Length; ++i)
                {
                    changeFrame(i, -1);
                }
            }
            else
            {
                changeFrame(input, -1);
            }
            applyProperties();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int input = button.Name[button.Name.Length - 1] - '0';

            if (mats[input] == null)
            {
                return;
            }

            var dialog = new SaveFileDialog();
            dialog.DefaultExt = ".png";
            dialog.Filter = "PNG | *.png";

            var res = dialog.ShowDialog(this);
            if (res.HasValue && res.Value)
            {
                var file = dialog.FileName;
                mats[input].Save(file);
            }
        }

        private void invertBox_Checked(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            int input = box.Name[box.Name.Length - 1] - '0';

            if (properties != null)
            {
                properties[input].inverted = true;
                applyProperties();
            }
        }

        private void invertBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            int input = box.Name[box.Name.Length - 1] - '0';

            if (properties != null)
            {
                properties[input].inverted = false;
                applyProperties();
            }
        }

        private void expandButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int input = button.Name[button.Name.Length - 1] - '0';
            if (mats[input] != null)
            {
                CvInvoke.Imshow(input.ToString(), mats[input]);
            }
        }
    }
}
