namespace BigGustave
{   
    using System;
    internal class Palette
    {
        public byte[] Data { get; set;}
        public byte[] Transparency { get; set;}

        public Palette(byte[] data, byte[] transparency)
        {
            Data = data;
            Transparency = transparency;

        }

        public Pixel GetPixel(int index, byte[] imgdat)
        {
            var start = (imgdat[index % imgdat.Length] % (Data.Length / 3)) * 3;

            byte[] pix = {Data[start], Data[start + 1], Data[start + 2]};

            byte trns = 255;

            // Console.WriteLine(Transparency[0] + ", " + Transparency[1] + ", " + Transparency[2]);

            if(Transparency != null){
                if(pix[0] == Transparency[0] &&
                   pix[1] == Transparency[1] &&
                   pix[2] == Transparency[2]){
                    trns = 0;
                }
            }

            return new Pixel(Data[start],
                Data[start + 1],
                Data[start + 2], trns, false);
        }
    }
}