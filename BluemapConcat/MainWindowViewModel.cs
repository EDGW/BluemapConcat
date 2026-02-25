using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace BluemapConcat
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] string mapAddress = "", tilePath = "maps/{world}/tiles/1/x{x}/z{z}.png", worldName = "overworld", errMsg = "";
        [ObservableProperty] int startX = 0, startZ = 0, endX = 1, endZ = 1;
        [ObservableProperty] int current, total, interval = 16, radius = 1, fitL = 65, fitH = 90, opacity = 70;
        [ObservableProperty] bool running, contour = false, bestFit = true, lower = false, upper = false, fit = true;
        [ObservableProperty] Color lowerC = Color.FromArgb(0xff, 0x00, 0x5c, 0xff), upperC = Color.FromArgb(0xff, 0xff, 0x00, 0x00), fitC = Color.FromArgb(0xff, 0x0, 0xff, 0x38);
        [ObservableProperty] WriteableBitmap? composite;
        IReadOnlyDictionary<(int, int), BitmapImage> _data = new Dictionary<(int, int), BitmapImage>();

        [RelayCommand]
        async Task FetchData()
        {
            Dictionary<(int, int), BitmapImage> maps = [];
            if (StartX > EndX || StartZ > EndZ)
            {
                return;
            }
            Current = 0;
            Total = (EndX - StartX + 1) * (EndZ - StartZ + 1);
            try
            {
                Running = true;
                for (int x = StartX; x <= EndX; x++) for (int z = StartZ; z <= EndZ; z++)
                {
                    try
                    {
                        var uri = new Uri(new Uri(MapAddress), TilePath.Replace("{x}", x.ToString()).Replace("{z}", z.ToString()).Replace("{world}", WorldName));
                        Console.WriteLine($"Fetching tile ({x},{z}) from {uri}...");
                        HttpClient client = new HttpClient();
                        var bytes = await client.GetByteArrayAsync(uri);
                        using var stream = new MemoryStream(bytes);
                        BitmapImage image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                        image.StreamSource = stream;
                        image.EndInit();
                        maps[(x, z)] = image;
                        Console.WriteLine($"Fetched tile ({x},{z}).");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching tile ({x},{z}): {ex.GetType()}");
                    }
                    Current++;
                }
            }
            catch (Exception ex)
            {
                ErrMsg = ex.Message;
            }
            finally
            {
                Running = false;
            }
            _data = maps;
        }

        [RelayCommand]
        void CreateMap()
        {
            var maps = _data;
            var processed = new Dictionary<(int, int), WriteableBitmap>();
            if (StartX > EndX || StartZ > EndZ)
            {
                return;
            }
            try
            {
                for (int x = StartX; x <= EndX; x++) for (int z = StartZ; z <= EndZ; z++)
                {
                    if (maps.ContainsKey((x, z))){
                        WriteableBitmap wb = Draw(maps[(x, z)], Interval, Radius);
                        processed[(x, z)] = wb;
                    }
                }
                var tileSize = 501;
                var tilesWide = EndX - StartX + 1;
                var tilesHigh = EndZ - StartZ + 1;
                var totalWidth = tilesWide * tileSize;
                var totalHeight = tilesHigh * tileSize;

                var composite = new WriteableBitmap(totalWidth, totalHeight, 96, 96, PixelFormats.Bgra32, null);
                var stride = tileSize * 4;
                var buffer = new byte[tileSize * stride];

                foreach (var entry in processed)
                {
                    var tile = entry.Value;
                    var offsetX = (entry.Key.Item1 - StartX) * tileSize;
                    var offsetZ = (entry.Key.Item2 - StartZ) * tileSize;

                    tile.CopyPixels(buffer, stride, 0);
                    composite.WritePixels(new Int32Rect(offsetX, offsetZ, tileSize, tileSize), buffer, stride, 0);
                }

                processed.Clear();
                this.Composite = composite;
            }
            catch (Exception ex)
            {
                ErrMsg = ex.Message;
            }
        }


        public static (byte b, byte g, byte r, byte a) GetPixel(int width, byte[] pixels, int x, int y)
        {
            int stride = width * 4;
            int row = y * stride;
            int offset = x * 4;
            byte tb = pixels[row + offset + 0];
            byte tg = pixels[row + offset + 1];
            byte tr = pixels[row + offset + 2];
            byte ta = pixels[row + offset + 3];
            return (tb, tg, tr, ta);
        }
        public static void SetPixel(int width, byte[] pixels, int x, int y, (byte b, byte g, byte r, byte a) pixel)
        {
            int stride = width * 4;
            int row = y * stride;
            int offset = x * 4;
            pixels[row + offset + 0] = pixel.b;
            pixels[row + offset + 1] = pixel.g;
            pixels[row + offset + 2] = pixel.r;
            pixels[row + offset + 3] = pixel.a;
        }
        public static (byte b, byte g, byte r, byte a) BlendColor((byte b, byte g, byte r, byte a) add, (byte b, byte g, byte r, byte a) bas)
        {
            float aTop = add.a / 255f;
            float aBottom = bas.a / 255f;
            float outA = aTop + aBottom * (1f - aTop);

            byte outR;
            byte outG;
            byte outB;

            if (outA <= 0f)
            {
                outR = outG = outB = 0;
            }
            else
            {
                float r = (add.r * aTop + bas.r * aBottom * (1f - aTop)) / outA;
                float g = (add.g * aTop + bas.g * aBottom * (1f - aTop)) / outA;
                float b = (add.b * aTop + bas.b * aBottom * (1f - aTop)) / outA;

                outR = (byte)Math.Clamp((int)Math.Round(r), 0, 255);
                outG = (byte)Math.Clamp((int)Math.Round(g), 0, 255);
                outB = (byte)Math.Clamp((int)Math.Round(b), 0, 255);

            }
            return (outB, outG, outR, (byte)Math.Clamp((int)Math.Round(outA * 255f), 0, 255));
        }

        public WriteableBitmap Draw(BitmapSource source, int sec = 32, int radius = 1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var resized = source;
            if (source.PixelWidth != 501 || source.PixelHeight != 1002)
            {
                var scaleX = 501d / source.PixelWidth;
                var scaleY = 1002d / source.PixelHeight;
                resized = new WriteableBitmap(new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY)));
            }

            var formatted = resized.Format == PixelFormats.Bgra32
                ? resized
                : new WriteableBitmap(new FormatConvertedBitmap(resized, PixelFormats.Bgra32, null, 0));

            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int halfHeight = height / 2;
            int stride = width * 4;

            var pixels = new byte[height * stride];
            formatted.CopyPixels(pixels, stride, 0);

            var result = new byte[halfHeight * stride];

            for (int y = 0; y < halfHeight; y++)
            {

                for (int x = 0; x < width; x++)
                {
                    var add = GetPixel(width, pixels, x, y);

                    var bas = GetPixel(width, pixels, x, y + halfHeight);
                    if (Contour)
                    {
                        const int divisor = 4;
                        if (bas.b % sec == 0)
                        {
                            SetPixel(width, result, x, y, ((byte)(add.b / divisor), (byte)(add.g / divisor), (byte)(add.r / divisor), 255));
                            continue;
                        }
                        var min = byte.MaxValue;
                        var max = byte.MinValue;
                        for (int xx = Math.Max(0, x - radius); xx < Math.Min(x + radius + 1, width); xx++)
                        {
                            for (int yy = Math.Max(0, y - radius); yy < Math.Min(y + radius + 1, halfHeight); yy++)
                            {
                                var (b, g, r, a) = GetPixel(width, pixels, xx, yy + halfHeight);
                                if (b < min)
                                {
                                    min = b;
                                }
                                if (b > max)
                                {
                                    max = b;
                                }
                            }
                        }
                        var mo8 = max / sec * sec;
                        if (max >= mo8 && min <= mo8)
                        {
                            SetPixel(width, result, x, y, ((byte)(add.b / divisor), (byte)(add.g / divisor), (byte)(add.r / divisor), 255));
                            continue;
                        }
                    }
                    var color = BlendColor(add, bas);
                    if (BestFit && Fit && bas.b >= FitL && bas.b <= FitH)
                    {
                        var clr = FitC;
                        (byte, byte, byte, byte) blend = (clr.B, clr.G, clr.R, (byte)Opacity);
                        color = BlendColor(blend, color);
                    }
                    if (BestFit && Lower && bas.b < FitL)
                    {
                        var clr = LowerC;
                        (byte, byte, byte, byte) blend = (clr.B, clr.G, clr.R, (byte)Opacity);
                        color = BlendColor(blend, color);
                    }
                    if (BestFit && Upper && bas.b > FitH)
                    {
                        var clr = UpperC;
                        (byte, byte, byte, byte) blend = (clr.B, clr.G, clr.R, (byte)Opacity);
                        color = BlendColor(blend, color);
                    }
                    SetPixel(width, result, x, y, color);
                }
            }
            var output = new WriteableBitmap(width, halfHeight, formatted.DpiX, formatted.DpiY, PixelFormats.Bgra32, null);
            output.WritePixels(new Int32Rect(0, 0, width, halfHeight), result, stride, 0);
            return output;
        }
        [RelayCommand]
        void XFromPos()
        {
            var vm = new FromPosDialogViewModel(StartX, EndX);
            if (new FromPosDialog() { DataContext = vm }.ShowDialog() ?? false)
            {
                StartX = (sbyte)vm.X1;
                EndX = (sbyte)vm.X2;
            }
        }
        [RelayCommand]
        void ZFromPos()
        {
            var vm = new FromPosDialogViewModel(StartZ, EndZ);
            if (new FromPosDialog() { DataContext = vm }.ShowDialog() ?? false)
            {
                StartZ = (sbyte)vm.X1;
                EndZ = (sbyte)vm.X2;
            }
        }
        [RelayCommand]
        void SaveMap()
        {
            if (Composite is null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save Map",
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap Image (*.bmp)|*.bmp",
                DefaultExt = ".png",
                AddExtension = true,
                FileName = "map"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            BitmapEncoder encoder = dialog.FilterIndex switch
            {
                2 => new JpegBitmapEncoder(),
                3 => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(Composite));

            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
    }
}