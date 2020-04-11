namespace BigGustave
{
    using System;

    internal static class Decoder
    {
        public static (byte bytesPerPixel, byte samplesPerPixel) GetBytesAndSamplesPerPixel(ImageHeader header)
        {   
            Console.WriteLine(header.BitDepth + " VOILA");

            // why correcting the bit depth?
            var bitDepthCorrected = (header.BitDepth + 7) / 8;

            var samplesPerPixel = SamplesPerPixel(header);

            // return ((byte)(samplesPerPixel * header.BitDepth), samplesPerPixel);
            return ((byte)(samplesPerPixel * bitDepthCorrected), samplesPerPixel);
        }

        public static byte[] Decode(byte[] decompressedData, ImageHeader header, byte bytesPerPixel, byte samplesPerPixel)
        {
            switch (header.InterlaceMethod)
            {
                case InterlaceMethod.None:
                    {
                        var bytesPerScanline = (int)BytesPerScanline(header, samplesPerPixel);

                        var currentRowStartByteAbsolute = 0;

                        for (var rowIndex = 0; rowIndex < header.Height; rowIndex++)
                        {
                            var filterType = (FilterType)decompressedData[currentRowStartByteAbsolute - 1];

                            var previousRowStartByteAbsolute = (bytesPerScanline * (rowIndex - 1));

                            var end = currentRowStartByteAbsolute + bytesPerScanline;
                            for (var currentByteAbsolute = currentRowStartByteAbsolute; currentByteAbsolute < end; currentByteAbsolute++)
                            {
                                ReverseFilter(decompressedData, filterType, previousRowStartByteAbsolute, currentRowStartByteAbsolute, currentByteAbsolute, currentByteAbsolute - currentRowStartByteAbsolute, bytesPerPixel);
                            }

                            currentRowStartByteAbsolute += bytesPerScanline;
                        }

                        return decompressedData;
                    }
                case InterlaceMethod.Adam7:
                    {
                        var pixelsPerRow = header.Width * bytesPerPixel;
                        var newBytes = new byte[header.Height * pixelsPerRow];
                        var i = 0;
                        var previousStartRowByteAbsolute = -1;
                        // 7 passes
                        for (var pass = 0; pass < 7; pass++)
                        {
                            var numberOfScanlines = Adam7.GetNumberOfScanlinesInPass(header, pass);
                            var numberOfPixelsPerScanline = Adam7.GetPixelsPerScanlineInPass(header, pass);

                            if (numberOfScanlines <= 0 || numberOfPixelsPerScanline <= 0)
                            {
                                continue;
                            }

                            for (var scanlineIndex = 0; scanlineIndex < numberOfScanlines; scanlineIndex++)
                            {
                                var filterType = (FilterType)decompressedData[i++];
                                var rowStartByte = i;

                                for (var j = 0; j < numberOfPixelsPerScanline; j++)
                                {
                                    var pixelIndex = Adam7.GetPixelIndexForScanlineInPass(header, pass, scanlineIndex, j);
                                    for (var k = 0; k < bytesPerPixel; k++)
                                    {
                                        var byteLineNumber = (j * bytesPerPixel) + k;
                                        ReverseFilter(decompressedData, filterType, previousStartRowByteAbsolute, rowStartByte, i, byteLineNumber, bytesPerPixel);
                                        i++;
                                    }

                                    var start = pixelsPerRow * pixelIndex.y + pixelIndex.x * bytesPerPixel;
                                    Array.ConstrainedCopy(decompressedData, (rowStartByte + j * bytesPerPixel) % decompressedData.Length, newBytes, start, bytesPerPixel);
                                }

                                previousStartRowByteAbsolute = rowStartByte;
                            }
                        }

                        return newBytes;
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Invalid interlace method: {header.InterlaceMethod}.");
            }
        }
        
        private static byte SamplesPerPixel(ImageHeader header)
        {
            switch (header.ColorType)
            {
                case ColorType.None: //Color type = 0
                    return 1;
                case ColorType.ColorUsed: // Color type = 2
                    return 3;
                case ColorType.ColorUsed | ColorType.PaletteUsed: // Color type = 3
                    return 1;
                case ColorType.AlphaChannelUsed: // Color type = 4 (Grayscale + alpha)
                    return 2;
                case ColorType.ColorUsed | ColorType.AlphaChannelUsed: // Color type = 6
                    return 4;
                default:
                    throw new ArgumentOutOfRangeException($"Invalid Color Type, color type was: {header.ColorType}.");
            }
        }

        //Will this only work with paletted images
        private static int BytesPerScanline(ImageHeader header, byte samplesPerPixel)
        {
            var width = header.Width;

            switch (header.BitDepth)
            {
                case 1:
                    return (width + 7) / 8;
                case 2:
                    return (width + 3) / 4;
                case 4:
                    return (width + 1) / 2;
                case 8:
                case 16:
                    return width * samplesPerPixel * (header.BitDepth / 8);
                default:
                    return 0;
            }
        }

        private static void ReverseFilter(byte[] data, FilterType type, int previousRowStartByteAbsolute, int rowStartByteAbsolute, int byteAbsolute, int rowByteIndex, int bytesPerPixel)
        {
            byte GetLeftByteValue()
            {
                var leftIndex = rowByteIndex - bytesPerPixel;
                var leftValue = leftIndex >= 0 ? data[(rowStartByteAbsolute + leftIndex)  % data.Length] : (byte)0;
                return leftValue;
            }

            byte GetAboveByteValue()
            {
                var upIndex = previousRowStartByteAbsolute + rowByteIndex;
                return upIndex >= 0 ? data[upIndex % data.Length] : (byte)0;
            }

            byte GetAboveLeftByteValue()
            {
                var index = previousRowStartByteAbsolute + rowByteIndex - bytesPerPixel;
                return index < previousRowStartByteAbsolute || previousRowStartByteAbsolute < 0 ? (byte)0 : data[index % data.Length];
            }

            // Moved out of the switch for performance.
            if (type == FilterType.Up)
            {
                var above = previousRowStartByteAbsolute + rowByteIndex;
                if (above < 0)
                {
                    return;
                }

                data[byteAbsolute % data.Length] += data[above % data.Length];
                return;
            }
            
            if (type == FilterType.Sub)
            {
                var leftIndex = rowByteIndex - bytesPerPixel;
                if (leftIndex < 0)
                {
                    return;
                }

                data[byteAbsolute % data.Length] += data[(rowStartByteAbsolute + leftIndex) % data.Length];
                return;
            }

            switch (type)
            {
                case FilterType.None:
                    return;
                case FilterType.Average:
                    data[byteAbsolute % data.Length] += (byte)((GetLeftByteValue() + GetAboveByteValue()) / 2);
                    break;
                case FilterType.Paeth:
                    var a = GetLeftByteValue();
                    var b = GetAboveByteValue();
                    var c = GetAboveLeftByteValue();
                    data[byteAbsolute % data.Length] += GetPaethValue(a, b, c);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// Computes a simple linear function of the three neighboring pixels (left, above, upper left),
        /// then chooses as predictor the neighboring pixel closest to the computed value.
        /// </summary>
        private static byte GetPaethValue(byte a, byte b, byte c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);

            if (pa <= pb && pa <= pc)
            {
                return a;
            }

            return pb <= pc ? b : c;
        }

        public static byte[] UnpackBytes(byte[] bytesIn, int bitDepth){
            byte[] bytesOut = new byte[bytesIn.Length * (8 / bitDepth)];

            switch(bitDepth){
                case 8:
                    bytesOut = bytesIn;
                    break;
                case 4:
                    for(var i = 0; i < bytesIn.Length; i++){
                        bytesOut[i * 2] = (byte)(bytesIn[i] >> 4);
                        bytesOut[i * 2 + 1] = (byte)(bytesIn[i] & 0x0f);
                    }
                    break;
                case 2:
                    for(var i = 0; i < bytesIn.Length; i++){
                        bytesOut[i * 4] = 0;
                        bytesOut[i * 4 + 1] = 0;
                        bytesOut[i * 4 + 2] = 0;
                        bytesOut[i * 4 + 3] = 0;
                    }
                    break;
                case 1:
                    for(var i = 0; i < bytesIn.Length; i++){
                        bytesOut[i * 8] = 0;
                        bytesOut[i * 8 + 1] = 0;
                        bytesOut[i * 8 + 2] = 0;
                        bytesOut[i * 8 + 3] = 0;
                        bytesOut[i * 8 + 4] = 0;
                        bytesOut[i * 8 + 5] = 0;
                        bytesOut[i * 8 + 6] = 0;
                        bytesOut[i * 8 + 7] = 0;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Invalid bit depth: {bitDepth}.");
            }

            return bytesOut;

        }

        public static byte[] RemoveBlackPixels(byte[] bytesOut, ImageHeader imageHeader){
            int j = 0;
            byte[] noBlackPixel = new byte[(imageHeader.Width * imageHeader.Height) / (8 / imageHeader.BitDepth)];

            for(var i = 0; i < bytesOut.Length; i++){
                if(i % (imageHeader.Width / (8 / imageHeader.BitDepth)) == 0){
                } else{
                    noBlackPixel[j] = bytesOut[i];
                    j++;
                }
            }

            return noBlackPixel;
        }
    }
}
