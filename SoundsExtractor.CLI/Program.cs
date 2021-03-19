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
        private const int SampleRate = 44100;

        private const double SampleLength = 0.2;

        private const int ResizedImageSideLength = 100;

        private const string JpgExtension = ".jpg";

        private const string WawExtension = ".wav";

        static void Main(string[] args)
        {
            if (!AreArgumentsValid(args))
                return;

            var inputFilePath = args[0];
            var outputFilePath = args[1];

            Console.WriteLine("Compressing image file...");
            var resizedBitmap = Resize(GetBitmap(inputFilePath), ResizedImageSideLength, ResizedImageSideLength);
            var compressedImagePath = CompressBitmap(resizedBitmap, inputFilePath);

            Console.WriteLine("Extracting sounds...");
            var brightnessSequence = GetBrightnessSequence(GetBitmap(compressedImagePath));
            var sampleChunks = GetSampleFrequencyChunks(brightnessSequence);

            Console.WriteLine($"Saving result to \"{outputFilePath}\" file");
            WriteToWav(sampleChunks, outputFilePath);
            File.Delete(compressedImagePath);

            var defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Extraction completed.");
            Console.ForegroundColor = defaultColor;
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

        private static bool AreArgumentsValid(string[] args) 
        {
            bool result = true;
            string resultMessage = "Arguments are valid.";

            if (args.Length < 2 || args.Any(a => string.IsNullOrWhiteSpace(a)))
            {
                result = false;
                resultMessage = "Please, provide not empty 2 arguments.";
            } 
            else if (!File.Exists(args[0]))
            {
                result = false;
                resultMessage = $"{args[0]} file is not exists.";
            } 
            else if (!string.Equals(Path.GetExtension(args[0]), JpgExtension, StringComparison.OrdinalIgnoreCase) ||
                       !string.Equals(Path.GetExtension(args[1]), WawExtension, StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                resultMessage = $"Unsupported file extension: must be \"{JpgExtension}\" for image file and \"{WawExtension}\" for sound file." +
                    "\nImage file name must be the first argument and sound file name - the second.";
            } 
            else if (!Path.IsPathRooted(args[0]) || !Path.IsPathRooted(args[1])) 
            {
                result = false;
                resultMessage = "Relative paths are not supported.";
            }

            var defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = result ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(resultMessage);
            Console.ForegroundColor = defaultColor;

            return result;
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
