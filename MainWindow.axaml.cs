using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MyAvaloniaApp;

public partial class MainWindow : Window
{
    private Point? previousPoint;      //последняя точка мыши для рисования линии
    private SKBitmap? skBitmap;        //основное изображение
    private SKCanvas? skCanvas;        //холст для рисования поверх изображения
    private SKPaint paint;             //настройки кисти
    private WriteableBitmap? avaloniaBitmap; //копия для отображения в Avalonia UI

    public MainWindow()
    {
        InitializeComponent();
        paint = new SKPaint //стиль кисти
        {
            Color = SKColors.Black,
            StrokeWidth = 4,
            IsAntialias = true
        };
    }

    private async void OpenImage_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions //кросс-платформенное диалоговое окно
        {
            Title = "Открыть изображение",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Изображения")
                {
                    Patterns = new[] { "*.bmp", "*.jpg", "*.jpeg", "*.png", "*.gif", "*.tiff", "*.ico" }
                }
            }
        });
        if (files.Count > 0)
        {
            //декодирование в SkiaSharp
            using var stream = await files[0].OpenReadAsync();
            skBitmap = SKBitmap.Decode(stream);
            
            if (skBitmap != null)
            {
                avaloniaBitmap = new WriteableBitmap(
                    new PixelSize(skBitmap.Width, skBitmap.Height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
                UpdateCanvasImage();
                skCanvas = new SKCanvas(skBitmap);
                ImageCanvas.Width = skBitmap.Width;
                ImageCanvas.Height = skBitmap.Height;
            }
        }
    }
    private async void SaveImage_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (skBitmap == null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить как...",
            DefaultExtension = ".png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("JPEG") { Patterns = new[] { "*.jpg" } },
                new FilePickerFileType("BMP") { Patterns = new[] { "*.bmp" } },
                new FilePickerFileType("GIF") { Patterns = new[] { "*.gif" } }
            }
        });
        if (file != null)
        {
            using var stream = await file.OpenWriteAsync();
            var format = Path.GetExtension(file.Path.LocalPath).ToLower() switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".bmp" => SKEncodedImageFormat.Bmp,
                ".gif" => SKEncodedImageFormat.Gif,
                _ => SKEncodedImageFormat.Png
            };
            skBitmap.Encode(stream, format, 100);
        }
    }
    private void Grayscale_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (skBitmap == null) return;
        for (int x = 0; x < skBitmap.Width; x++)
        for (int y = 0; y < skBitmap.Height; y++)
        {
            var pixel = skBitmap.GetPixel(x, y);
            var gray = (byte)(0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);
            skBitmap.SetPixel(x, y, new SKColor(gray, gray, gray));
        }
        UpdateCanvasImage();
    }
    private void OnMouseDown(object sender, PointerPressedEventArgs e)
    {
        var position = e.GetPosition(ImageCanvas);
        previousPoint = new Point((float)position.X, (float)position.Y);
    }
    private void OnMouseMove(object sender, PointerEventArgs e)
    {
        if (skCanvas == null || previousPoint == null) return;
        var pointerPoint = e.GetCurrentPoint(ImageCanvas);
        if (!pointerPoint.Properties.IsLeftButtonPressed) return;
        var position = e.GetPosition(ImageCanvas);
        var point = new Point((float)position.X, (float)position.Y);
        skCanvas.DrawLine(
            new SKPoint((float)previousPoint.Value.X, (float)previousPoint.Value.Y),
            new SKPoint((float)point.X, (float)point.Y), 
            paint);

        previousPoint = point;
        UpdateCanvasImage();
    }

    private void UpdateCanvasImage()
    {
        if (skBitmap == null || avaloniaBitmap == null) return;
        using var locked = avaloniaBitmap.Lock();
        using var surface = SKSurface.Create(new SKImageInfo(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888));
        surface.Canvas.DrawBitmap(skBitmap, 0, 0);
        surface.Snapshot().ReadPixels(
            new SKImageInfo(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888), 
            locked.Address, 
            locked.RowBytes);
        Dispatcher.UIThread.Post(() =>
        {
            var imageBrush = new ImageBrush(avaloniaBitmap) { Stretch = Stretch.None };
            ImageCanvas.Background = imageBrush;
        });
    }
}
