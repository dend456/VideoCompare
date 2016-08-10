using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VidCompare
{
    public enum InputFormat
    {
        Video,
        Image
    }

    public class VideoProperties
    {
        public InputFormat format = InputFormat.Image;
        public int frame = 0;
        public int contrast = 0;
        public int brightness = 0;
        public bool inverted = false;
        public bool grayscale = false;

        public void clear()
        {
            contrast = 0;
            brightness = 0;
            inverted = false;
            grayscale = false;
        }
    }
}
