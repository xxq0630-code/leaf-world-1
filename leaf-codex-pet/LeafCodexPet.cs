using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LeafCodexPet
{
    public sealed class PetApp : Application
    {
        [STAThread]
        public static void Main()
        {
            PetApp app = new PetApp();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new PetWindow());
        }
    }

    public sealed class PetWindow : Window
    {
        private const double BaseWidth = 280.0;
        private const double BaseHeight = 370.0;

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly DispatcherTimer frameTimer = new DispatcherTimer();
        private readonly Random random = new Random();
        private readonly ScaleTransform characterScale = new ScaleTransform(1.0, 1.0);
        private readonly RotateTransform characterRotation = new RotateTransform(0.0);
        private readonly TranslateTransform characterTranslation = new TranslateTransform(0.0, 0.0);
        private readonly ScaleTransform uiScale = new ScaleTransform(1.0, 1.0);
        private readonly Canvas eyelids = new Canvas();
        private readonly Border speechBubble = new Border();
        private readonly TextBlock speechText = new TextBlock();

        private double lastFrame;
        private double nextBlink;
        private double blinkStart = -1.0;
        private double speechUntil;
        private double nextWander;
        private double wanderTarget;
        private double nextSip;
        private double sipStart = -1.0;
        private int facing = 1;
        private bool walking;
        private bool wanderEnabled = true;
        private bool paused;
        private bool chatty;
        private bool dragging;
        private bool pointerDown;
        private Point dragScreenStart;
        private Point dragWindowStart;

        private readonly string[] phrases = new string[]
        {
            "我没笑。程序跑通了。",
            "继续。我在听。",
            "橙汁比会议可靠。",
            "没有情绪，只是默认脸。",
            "先保存，再大胆。",
            "看起来没动，其实在思考。",
            "问题不大。先看日志。"
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);

        public PetWindow()
        {
            Title = "leaf codex-pet";
            Width = BaseWidth;
            Height = BaseHeight;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            Focusable = true;
            SnapsToDevicePixels = true;

            Grid root = BuildInterface();
            root.LayoutTransform = uiScale;
            Content = root;
            ContextMenu = BuildContextMenu();

            Loaded += OnLoaded;
            Closed += delegate { frameTimer.Stop(); };
            MouseLeftButtonDown += OnPointerDown;
            MouseMove += OnPointerMove;
            MouseLeftButtonUp += OnPointerUp;

            frameTimer.Interval = TimeSpan.FromMilliseconds(33.0);
            frameTimer.Tick += OnFrame;
        }

        private Grid BuildInterface()
        {
            Grid root = new Grid();

            Grid character = new Grid();
            character.Width = 160.0;
            character.Height = 320.0;
            character.HorizontalAlignment = HorizontalAlignment.Center;
            character.VerticalAlignment = VerticalAlignment.Bottom;
            character.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
            character.RenderTransformOrigin = new Point(0.5, 0.86);

            TransformGroup motion = new TransformGroup();
            motion.Children.Add(characterScale);
            motion.Children.Add(characterRotation);
            motion.Children.Add(characterTranslation);
            character.RenderTransform = motion;

            Image sprite = new Image();
            sprite.Source = LoadEmbeddedPng("DeadpanPet.Character.png");
            sprite.Stretch = Stretch.Fill;
            sprite.Width = 160.0;
            sprite.Height = 320.0;
            sprite.HorizontalAlignment = HorizontalAlignment.Center;
            sprite.VerticalAlignment = VerticalAlignment.Center;
            RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.NearestNeighbor);
            character.Children.Add(sprite);

            BuildEyelids();
            character.Children.Add(eyelids);
            root.Children.Add(character);

            speechText.Text = "";
            speechText.Foreground = Brushes.White;
            speechText.FontFamily = new FontFamily("Microsoft YaHei UI");
            speechText.FontSize = 14.0;
            speechText.FontWeight = FontWeights.SemiBold;
            speechText.TextWrapping = TextWrapping.Wrap;
            speechText.TextAlignment = TextAlignment.Center;
            speechText.MaxWidth = 214.0;

            speechBubble.Child = speechText;
            speechBubble.Background = new SolidColorBrush(Color.FromArgb(238, 25, 27, 32));
            speechBubble.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 86, 91, 102));
            speechBubble.BorderThickness = new Thickness(1.0);
            speechBubble.CornerRadius = new CornerRadius(14.0);
            speechBubble.Padding = new Thickness(13.0, 9.0, 13.0, 9.0);
            speechBubble.HorizontalAlignment = HorizontalAlignment.Center;
            speechBubble.VerticalAlignment = VerticalAlignment.Top;
            speechBubble.Margin = new Thickness(8.0, 4.0, 8.0, 0.0);
            speechBubble.Visibility = Visibility.Collapsed;
            speechBubble.IsHitTestVisible = false;
            speechBubble.Effect = new DropShadowEffect
            {
                BlurRadius = 12.0,
                ShadowDepth = 3.0,
                Opacity = 0.34,
                Color = Colors.Black
            };
            Panel.SetZIndex(speechBubble, 10);
            root.Children.Add(speechBubble);

            return root;
        }

        private void BuildEyelids()
        {
            eyelids.Width = 160.0;
            eyelids.Height = 320.0;
            eyelids.IsHitTestVisible = false;
            eyelids.Opacity = 0.0;

            AddEyelid(47.0, 78.0, 24.0, 0.0);
            AddEyelid(81.0, 78.0, 24.0, 0.0);
        }

        private void AddEyelid(double x, double y, double width, double angle)
        {
            Grid patch = new Grid();
            patch.Width = width;
            patch.Height = 14.0;
            patch.RenderTransformOrigin = new Point(0.5, 0.5);
            patch.RenderTransform = new RotateTransform(angle);

            Rectangle skin = new Rectangle();
            skin.Fill = new SolidColorBrush(Color.FromRgb(239, 169, 112));
            skin.Width = width;
            skin.Height = 12.0;
            patch.Children.Add(skin);

            Line lid = new Line();
            lid.X1 = 2.0;
            lid.X2 = width - 2.0;
            lid.Y1 = 8.0;
            lid.Y2 = 8.0;
            lid.Stroke = new SolidColorBrush(Color.FromRgb(62, 45, 39));
            lid.StrokeThickness = 4.0;
            lid.StrokeStartLineCap = PenLineCap.Square;
            lid.StrokeEndLineCap = PenLineCap.Square;
            patch.Children.Add(lid);

            Canvas.SetLeft(patch, x);
            Canvas.SetTop(patch, y);
            eyelids.Children.Add(patch);
        }

        private ContextMenu BuildContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.FontFamily = new FontFamily("Microsoft YaHei UI");

            MenuItem wander = new MenuItem();
            wander.Header = "允许小范围散步";
            wander.IsCheckable = true;
            wander.IsChecked = true;
            wander.Click += delegate
            {
                wanderEnabled = wander.IsChecked;
                walking = false;
                nextWander = clock.Elapsed.TotalSeconds + 2.0;
            };
            menu.Items.Add(wander);

            MenuItem pause = new MenuItem();
            pause.Header = "暂停动作";
            pause.IsCheckable = true;
            pause.Click += delegate
            {
                paused = pause.IsChecked;
                if (paused)
                {
                    walking = false;
                    characterScale.ScaleX = facing;
                    characterScale.ScaleY = 1.0;
                    characterRotation.Angle = 0.0;
                    characterTranslation.X = 0.0;
                    characterTranslation.Y = 0.0;
                }
            };
            menu.Items.Add(pause);

            MenuItem chat = new MenuItem();
            chat.Header = "偶尔主动说一句";
            chat.IsCheckable = true;
            chat.Click += delegate { chatty = chat.IsChecked; };
            menu.Items.Add(chat);

            menu.Items.Add(new Separator());

            MenuItem size = new MenuItem();
            size.Header = "大小";
            size.Items.Add(MakeSizeItem("小", 0.80));
            size.Items.Add(MakeSizeItem("标准", 1.00));
            size.Items.Add(MakeSizeItem("大", 1.25));
            menu.Items.Add(size);

            MenuItem reset = new MenuItem();
            reset.Header = "回到右下角";
            reset.Click += delegate { PlaceAtBottomRight(); };
            menu.Items.Add(reset);

            menu.Items.Add(new Separator());

            MenuItem exit = new MenuItem();
            exit.Header = "退出桌宠";
            exit.Click += delegate { Close(); };
            menu.Items.Add(exit);

            return menu;
        }

        private MenuItem MakeSizeItem(string label, double scale)
        {
            MenuItem item = new MenuItem();
            item.Header = label;
            item.Click += delegate { SetUiScale(scale); };
            return item;
        }

        private void SetUiScale(double scale)
        {
            Point center = new Point(Left + Width / 2.0, Top + Height);
            uiScale.ScaleX = scale;
            uiScale.ScaleY = scale;
            Width = BaseWidth * scale;
            Height = BaseHeight * scale;
            Left = center.X - Width / 2.0;
            Top = center.Y - Height;
            ClampToWorkArea();
        }

        private static BitmapSource LoadEmbeddedPng(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException("找不到内置角色素材：" + resourceName);

            using (stream)
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PlaceAtBottomRight();
            double now = clock.Elapsed.TotalSeconds;
            lastFrame = now;
            nextBlink = now + 1.6;
            nextWander = now + 5.5;
            nextSip = now + 12.0;
            frameTimer.Start();
            Speak("我没笑。只是启动了。", 3.2);
        }

        private void PlaceAtBottomRight()
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - Width - 20.0;
            Top = area.Bottom - Height - 12.0;
            walking = false;
            nextWander = clock.Elapsed.TotalSeconds + 5.0;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double delta = Math.Min(0.08, Math.Max(0.0, now - lastFrame));
            lastFrame = now;

            UpdateSpeech(now);
            UpdateBlink(now);

            if (paused || pointerDown)
                return;

            UpdateWander(now, delta);

            double breath = Math.Sin(now * Math.PI * 2.0 / 3.9);
            double sway = Math.Sin(now * Math.PI * 2.0 / 6.4) * 0.42;
            double bob = -Math.Sin(now * Math.PI * 2.0 / 3.9) * 1.15;
            double step = walking ? -Math.Abs(Math.Sin(now * 8.8)) * 2.3 : 0.0;
            double sip = UpdateSip(now);
            double cursorLean = GetCursorLean();

            characterScale.ScaleX = facing * (1.0 + (walking ? Math.Sin(now * 8.8) * 0.008 : 0.0));
            characterScale.ScaleY = 1.0 + breath * 0.0055;
            characterRotation.Angle = sway + cursorLean * 1.15 - sip * 2.1 * facing;
            characterTranslation.X = cursorLean * 1.5;
            characterTranslation.Y = bob + step - sip * 2.8;

            if (chatty && speechBubble.Visibility != Visibility.Visible && random.NextDouble() < delta / 32.0)
                SpeakRandom();
        }

        private void UpdateBlink(double now)
        {
            if (blinkStart < 0.0 && now >= nextBlink)
            {
                blinkStart = now;
                nextBlink = now + 2.8 + random.NextDouble() * 4.4;
            }

            if (blinkStart < 0.0)
            {
                eyelids.Opacity = 0.0;
                return;
            }

            double age = now - blinkStart;
            if (age >= 0.16)
            {
                eyelids.Opacity = 0.0;
                blinkStart = -1.0;
            }
            else
            {
                double triangle = age < 0.08 ? age / 0.08 : (0.16 - age) / 0.08;
                eyelids.Opacity = Math.Max(0.0, Math.Min(1.0, triangle * 1.7));
            }
        }

        private void UpdateWander(double now, double delta)
        {
            if (!wanderEnabled)
            {
                walking = false;
                return;
            }

            Rect area = SystemParameters.WorkArea;
            if (!walking && now >= nextWander)
            {
                double range = 90.0 + random.NextDouble() * 130.0;
                double direction = random.Next(2) == 0 ? -1.0 : 1.0;
                wanderTarget = Math.Max(area.Left + 6.0, Math.Min(area.Right - Width - 6.0, Left + range * direction));
                facing = wanderTarget >= Left ? 1 : -1;
                walking = Math.Abs(wanderTarget - Left) > 8.0;
                if (!walking)
                    nextWander = now + 5.0;
            }

            if (!walking)
                return;

            double remaining = wanderTarget - Left;
            double move = Math.Sign(remaining) * 27.0 * delta;
            if (Math.Abs(move) >= Math.Abs(remaining))
            {
                Left = wanderTarget;
                walking = false;
                nextWander = now + 7.0 + random.NextDouble() * 10.0;
            }
            else
            {
                Left += move;
            }
        }

        private double UpdateSip(double now)
        {
            if (sipStart < 0.0 && now >= nextSip && !walking)
            {
                sipStart = now;
                nextSip = now + 18.0 + random.NextDouble() * 18.0;
            }

            if (sipStart < 0.0)
                return 0.0;

            double age = now - sipStart;
            if (age >= 1.7)
            {
                sipStart = -1.0;
                return 0.0;
            }

            return Math.Sin(Math.PI * age / 1.7);
        }

        private double GetCursorLean()
        {
            NativePoint point;
            if (!GetCursorPos(out point))
                return 0.0;

            double center = Left + Width / 2.0;
            double horizontal = (point.X - center) / 420.0;
            return Math.Max(-1.0, Math.Min(1.0, horizontal));
        }

        private void UpdateSpeech(double now)
        {
            if (speechBubble.Visibility == Visibility.Visible && now >= speechUntil)
                speechBubble.Visibility = Visibility.Collapsed;
        }

        private void SpeakRandom()
        {
            Speak(phrases[random.Next(phrases.Length)], 3.0);
        }

        private void Speak(string text, double seconds)
        {
            speechText.Text = text;
            speechBubble.Visibility = Visibility.Visible;
            speechUntil = clock.Elapsed.TotalSeconds + seconds;
        }

        private void OnPointerDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (e.ClickCount >= 2)
            {
                Speak("没笑。只是双击成功了。", 3.0);
                return;
            }

            pointerDown = true;
            dragging = false;
            dragScreenStart = PointToScreen(e.GetPosition(this));
            dragWindowStart = new Point(Left, Top);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnPointerMove(object sender, MouseEventArgs e)
        {
            if (!pointerDown || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point current = PointToScreen(e.GetPosition(this));
            Vector delta = current - dragScreenStart;
            if (delta.Length > 3.0)
                dragging = true;

            if (dragging)
            {
                Left = dragWindowStart.X + delta.X;
                Top = dragWindowStart.Y + delta.Y;
            }
        }

        private void OnPointerUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !pointerDown)
                return;

            pointerDown = false;
            ReleaseMouseCapture();
            if (dragging)
            {
                ClampToWorkArea();
                walking = false;
                nextWander = clock.Elapsed.TotalSeconds + 7.0;
            }
            else
            {
                SpeakRandom();
            }
            e.Handled = true;
        }

        private void ClampToWorkArea()
        {
            Rect area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left - Width * 0.35, Math.Min(area.Right - Width * 0.65, Left));
            Top = Math.Max(area.Top, Math.Min(area.Bottom - Height * 0.45, Top));
        }
    }
}
