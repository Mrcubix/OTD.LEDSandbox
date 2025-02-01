using System.IO;

namespace OTD.LEDSandbox.Converters
{
    public abstract class Converter
    {
        /// <summary>
        ///   Converts the image bytes to a common format.
        /// </summary>
        /// <param name="data">The image's stream to convert.</param>
        /// <returns>The converted image bytes.</returns>
        public abstract byte[]? Convert(Stream data, bool doFlipVertically, bool doFlipHorizontally);

         /// <summary>
        ///   Finalizes the conversion.
        /// </summary>
        /// <param name="data">The data to be converted</param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <remarks>I can't understand these shenanigans.</remarks>
        /// <returns>The final converted image.</returns>
        protected virtual byte[] FinalizeConversion(byte[] data)
        {
            const int WIDTH = 64;
            const int HEIGHT = 32 * 4;

            byte[] convertedImg = new byte[WIDTH * HEIGHT];

            int x = 0;
            int y = 0;
            bool firstline = true;
            int counter = 1;

            for (int i = 0; i < data.Length; i++)
            {
                byte chr = data[i];
                byte h = (byte)((chr >> 4) & 0x0F);
                byte l = (byte)(chr & 0x0F);

                int k1 = counter;
                int k2 = counter + 2;
                convertedImg[k1] = h;
                convertedImg[k2] = l;

                counter += 4;
                x += 2;

                if (x >= WIDTH)
                {
                    y++;
                    x = 0;
                    if (firstline)
                    {
                        firstline = false;
                        counter -= WIDTH * 2 + 1;
                    }
                    else
                    {
                        firstline = true;
                        counter += 1;
                    }
                }
            }

            return convertedImg;
        }
    }
}