using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundsExtractor.CLI
{
    class Program
    {
        // Need to generate values:
        // Frequency (brightness)
        // SampleLength
        // sampleLengthSequenceCount - mb read several of it from picture parts 

        private const int SampleRate = 44100;
        private const double SampleLength = 0.2;

        private string _lowQualityImagePath = string.Empty;

        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Any(a => string.IsNullOrWhiteSpace(a)))
                throw new ArgumentException();

            var inputFilePath = File.Exists(args[0]) ? args[0] : throw new ArgumentException();
            var outputFilePath = args[1];

            var resizedBitmap = Resize(GetBitmap(inputFilePath), 100, 100);
            var compressedImagePath = CompressBitmap(resizedBitmap, inputFilePath); //CompressBitmap(inputFilePath);

            var brightnessSequence = GetBrightnessSequence(GetBitmap(compressedImagePath));
            var sampleChunks = GetSampleFrequencyChunks(brightnessSequence);

            WriteToWav(sampleChunks, outputFilePath);
        }

        private static void WriteToWav(List<(int Frequency, double Length)> sampleChunks, string filePath) 
        {
            using (var wfw = new WaveFileWriter(filePath, WaveFormat.CreateALawFormat(SampleRate, 2)))
            {
                foreach(var sampleChunk in sampleChunks)
                {
                    float[] data = new float[Convert.ToInt32(SampleRate * sampleChunk.Length)];
                    GetGenerator(sampleChunk.Frequency).Take(TimeSpan.FromSeconds(sampleChunk.Length)).Read(data, 0, data.Length);

                    var bytes = new List<byte>(data.Length * 4);

                    foreach (var item in data)
                    {
                        var itemBytes = BitConverter.GetBytes(item);

                        foreach (var receivedByte in itemBytes)
                            bytes.Add(receivedByte);
                    }

                    wfw.Write(bytes.ToArray(), 0, bytes.Count);
                }
            }
        }

        private static SignalGenerator GetGenerator(double frequency)
            => new SignalGenerator() { Frequency = frequency };

        private static List<(int Frequency, double Length)> GetSampleFrequencyChunks(int[] brightnessSequence) 
        {
            var result = new List<(int, double)>();

            var chunkLength = 10;

            for (int i = chunkLength; i < brightnessSequence.Length; i += chunkLength) 
            {
                var sum = 0;

                for (int j = i - chunkLength; j < i; j++)
                    sum += brightnessSequence[j];

                var avgFrequence = (sum / chunkLength) * 20;

                if(avgFrequence > 0)
                    result.Add((avgFrequence, 0.1d));
            }

            return result;
        }

        private static int[] GetBrightnessSequence(Bitmap image)
        {
            BitmapData bData =
                image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            int stride = bData.Stride;

            IntPtr scan0 = bData.Scan0;

            var result = new List<int>();

            unsafe
            {
                byte* p = (byte*)(void*)scan0;

                int nOffset = stride - image.Width * 3;

                for (int i = 0; i < image.Height; ++i)
                {
                    for (int j = 0; j < image.Width; ++j)
                    {
                        var averageBrightness = (p[0] + p[1] + p[2]) / 3;
                        result.Add(averageBrightness);

                        p += 3;
                    }

                    p += nOffset;
                }
            }

            return result.ToArray();
        }

        private static Bitmap Resize(Bitmap image, int width, int height)
            => new Bitmap(image, new Size(width, height));

        private static Bitmap GetBitmap(string fileName)
        {
            Bitmap img = null;

            using (FileStream fs = new FileStream(fileName, FileMode.Open))
                img = new Bitmap(fs);

            return img;
        }

        private static string CompressBitmap(Bitmap bitmap, string fileSavePath) 
        {
            var compressedImagePath = Path.Combine(Path.GetDirectoryName(fileSavePath), $"{Path.GetFileName(fileSavePath)}_compressed.jpg");

            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

            var myEncoder = Encoder.Quality;

            var myEncoderParameters = new EncoderParameters(1);

            var myEncoderParameter = new EncoderParameter(myEncoder, 10L);
            myEncoderParameters.Param[0] = myEncoderParameter;
            bitmap.Save(compressedImagePath, jpgEncoder, myEncoderParameters);

            return compressedImagePath;
        }

        private static string CompressBitmap(string imagePath)
        {
            var compressedImagePath = Path.Combine(Path.GetDirectoryName(imagePath), $"{Path.GetFileName(imagePath)}_compressed.jpg");

            using (Bitmap bmp = new Bitmap(imagePath))
            {
                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

                var myEncoder = Encoder.Quality;

                var myEncoderParameters = new EncoderParameters(1);

                var myEncoderParameter = new EncoderParameter(myEncoder, 0L);
                myEncoderParameters.Param[0] = myEncoderParameter;
                bmp.Save(compressedImagePath, jpgEncoder, myEncoderParameters);

                return compressedImagePath;
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
