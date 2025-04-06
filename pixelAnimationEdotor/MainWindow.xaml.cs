using PixelEditor.Managers;
using PixelEditor.Models;
using PixelEditor.Services;
using System.Collections.Generic;
using System.Drawing; 
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace PixelEditor
{
    public partial class MainWindow : Window
    {
        private const int PixelSize = 5;
        private FrameManager _frameManager = new();
        private CanvasRenderer _renderer = new();
        private UndoRedoManager _history = new();
        private ColorPaletteService _palette = new();
        private MediaColor _selectedColor = Colors.Black;
        private bool _isDrawing = false;
        private bool _isEraser = false;
        private int _brushSize = 1;

        public MainWindow()
        {
            InitializeComponent();
            _frameManager.AddFrame();
            InitColorPalette();
            UpdateFrameIndicator();
            _renderer.Render(PixelCanvas, _frameManager.Current.Canvas, PixelSize);

            PixelCanvas.MouseLeftButtonDown += PixelCanvas_MouseLeftButtonDown;
            PixelCanvas.MouseMove += PixelCanvas_MouseMove;
            PixelCanvas.MouseLeftButtonUp += PixelCanvas_MouseLeftButtonUp;
            BrushSizeSlider.ValueChanged += BrushSizeSlider_ValueChanged;
        }

        private void PixelCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            DrawAtMousePosition(e);
        }

        private void PixelCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
            {
                DrawAtMousePosition(e);
            }
        }

        private void PixelCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
        }

        private void DrawAtMousePosition(MouseEventArgs e)
        {
            var pos = e.GetPosition(PixelCanvas);
            int x = (int)(pos.X / PixelSize);
            int y = (int)(pos.Y / PixelSize);

            var canvas = _frameManager.Current.Canvas;
            _history.SaveState(canvas);

            for (int dx = -_brushSize / 2; dx <= _brushSize / 2; dx++)
            {
                for (int dy = -_brushSize / 2; dy <= _brushSize / 2; dy++)
                {
                    int px = x + dx;
                    int py = y + dy;
                    if (_isEraser)
                    {
                        canvas.Remove((px, py));
                    }
                    else
                    {
                        canvas[(px, py)] = _selectedColor;
                    }
                }
            }

            _renderer.Render(PixelCanvas, canvas, PixelSize);
        }

        private void AddFrame_Click(object sender, RoutedEventArgs e)
        {
            _frameManager.AddFrame(copyPrevious: true);
            _history.Clear();
            UpdateFrameIndicator();
            _renderer.Render(PixelCanvas, _frameManager.Current.Canvas, PixelSize);
        }

        private async void PlayAnimation_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _frameManager.Frames.Count; i++)
            {
                _frameManager.SetFrame(i);
                UpdateFrameIndicator();
                _renderer.Render(PixelCanvas, _frameManager.Current.Canvas, PixelSize);
                await Task.Delay(300);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            var result = _history.Undo(_frameManager.Current.Canvas);
            if (result != null)
            {
                _frameManager.Current.Canvas = result;
                _renderer.Render(PixelCanvas, _frameManager.Current.Canvas, PixelSize);
            }
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            var result = _history.Redo(_frameManager.Current.Canvas);
            if (result != null)
            {
                _frameManager.Current.Canvas = result;
                _renderer.Render(PixelCanvas, _frameManager.Current.Canvas, PixelSize);
            }
        }

        private void InitColorPalette()
        {
            var colors = _palette.GeneratePalette(24);
            
            var eraserButton = new Button
            {
                Width = 30,
                Height = 30,
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Red,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(2),
                ToolTip = "Eraser",
                Content = new TextBlock
                {
                    Text = "🧽", 
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            eraserButton.Click += (s, e) =>
            {
                _isEraser = true;
            };
            ColorPalette.Children.Add(eraserButton);


            foreach (var color in colors)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = new SolidColorBrush(color),
                    Stroke = System.Windows.Media.Brushes.Black,
                    StrokeThickness = 1,
                    Margin = new Thickness(2)
                };
                rect.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedColor = color;
                    _isEraser = false;
                };
                ColorPalette.Children.Add(rect);
            }
        }

        private void UpdateFrameIndicator()
        {
            FrameIndicator.Text = $"Frame: {_frameManager.CurrentIndex + 1} of {_frameManager.Frames.Count}";
        }

        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrushSizeLabel != null)
            {
                _brushSize = (int)e.NewValue;
                BrushSizeLabel.Text = _brushSize.ToString();
            }
        }

        private void SaveGif_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "GIF Image|*.gif" };
            if (dialog.ShowDialog() == true)
            {
                var encoder = new GifBitmapEncoder();
                foreach (var frame in _frameManager.Frames)
                {
                    var bmp = new Bitmap((int)PixelCanvas.Width, (int)PixelCanvas.Height);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(DrawingColor.White);
                        foreach (var pixel in frame.Canvas)
                        {
                            var color = frame.Canvas[pixel.Key];
                            using var brush = new SolidBrush(DrawingColor.FromArgb(color.A, color.R, color.G, color.B));
                            g.FillRectangle(brush, pixel.Key.Item1 * PixelSize, pixel.Key.Item2 * PixelSize, PixelSize, PixelSize);
                        }
                    }
                    using var ms = new MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    ms.Seek(0, SeekOrigin.Begin);
                    var decoder = new BmpBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    encoder.Frames.Add(decoder.Frames[0]);
                }
                using var fs = new FileStream(dialog.FileName, FileMode.Create);
                encoder.Save(fs);
            }
        }
    }
}
