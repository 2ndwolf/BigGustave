namespace BigGustave
{    using System;
    internal class Palette
    {
        public byte[] Data { get; }

        public Palette(byte[] data)
        {
            Data = data;
        }

        public Pixel GetPixel(int index, byte[] imgdat, int bytesPerPixel)
        {
            var start = imgdat[index % imgdat.Length] % (Data.Length / 3);


            return new Pixel(Data[start * 3],
                Data[start * 3 + 1],
                Data[start * 3 + 2], 255, false);
        }
    }
}