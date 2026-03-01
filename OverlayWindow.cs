using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Media;

namespace NatifyOverlay
{
    public class OverlayWindow : Window
    {
        private StackPanel _notificationPanel;
        private TextBox _replyBox;
        private Border _inputContainer;
        private DispatcherTimer _topmostRefresher;
        private DispatcherTimer _autoFixTimer;
        private bool _isInteractive = false;
        private bool _playSound = true;
        private IntPtr _gameHwnd = IntPtr.Zero;
        private HashSet<IntPtr> _fixedWindows = new HashSet<IntPtr>();
        
        private const int HOTKEY_ID_INTERACT = 9000;
        private const int HOTKEY_ID_BORDERLESS = 9001;

        // Theme Colors
        private SolidColorBrush _accentColor;
        private SolidColorBrush _cardBackground;
        private SolidColorBrush _inputBackground;
        private SolidColorBrush _inputFocusBackground;
        private SolidColorBrush _primaryText = Brushes.White;
        private SolidColorBrush _secondaryText = new SolidColorBrush(Color.FromRgb(220, 221, 222));
        private SolidColorBrush _borderColor;

        public OverlayWindow(string theme = "Discord", bool playSound = true)
        {
            ApplyTheme(theme);
            _playSound = playSound;

            this.Title = "NatifyOverlay";
            this.Width = 400;
            this.Height = 600;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.ShowActivated = false;
            
            InitUI();

            // Reduced refresh rate to 250ms to prevent CPU stutter
            _topmostRefresher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _topmostRefresher.Tick += (s, e) => ForceToFront();
            _topmostRefresher.Start();

            // Scan every 5 seconds instead of 3
            _autoFixTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _autoFixTimer.Tick += (s, e) => Task.Run(() => ScanForGames());
            _autoFixTimer.Start();

            this.Loaded += OnLoaded;
            this.Closed += OnClosed;

            PositionWindow();
        }

        private void ApplyTheme(string theme)
        {
            if (theme.Equals("Pink", StringComparison.OrdinalIgnoreCase))
            {
                _accentColor = new SolidColorBrush(Color.FromRgb(255, 105, 180)); // HotPink
                _cardBackground = new SolidColorBrush(Color.FromRgb(255, 240, 245)); // LavenderBlush
                _inputBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // White
                _inputFocusBackground = new SolidColorBrush(Color.FromRgb(255, 192, 203)); // Pink
                _primaryText = new SolidColorBrush(Color.FromRgb(75, 0, 130)); // Indigo
                _secondaryText = new SolidColorBrush(Color.FromRgb(105, 105, 105)); // DimGray
                _borderColor = new SolidColorBrush(Color.FromRgb(255, 182, 193)); // LightPink
            }
            else // Default Discord
            {
                _accentColor = new SolidColorBrush(Color.FromRgb(88, 101, 242)); // Blurple
                _cardBackground = new SolidColorBrush(Color.FromRgb(54, 57, 63)); // DarkGrey
                _inputBackground = new SolidColorBrush(Color.FromRgb(47, 49, 54)); // DarkerGrey
                _inputFocusBackground = new SolidColorBrush(Color.FromRgb(32, 34, 37)); // HeaderGrey
                _primaryText = Brushes.White;
                _secondaryText = new SolidColorBrush(Color.FromRgb(220, 221, 222));
                _borderColor = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            }
        }

        private void InitUI()
        {
            var mainGrid = new Grid();
            _notificationPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10) };
            _inputContainer = new Border
            {
                Background = _inputBackground, CornerRadius = new CornerRadius(5), Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10), Visibility = Visibility.Collapsed,
                BorderBrush = _borderColor, BorderThickness = new Thickness(1)
            };

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _replyBox = new TextBox
            {
                Background = _inputFocusBackground, Foreground = _primaryText, BorderThickness = new Thickness(0),
                Padding = new Thickness(5), FontSize = 14, FontFamily = new FontFamily("Segoe UI")
            };
            
            _replyBox.KeyDown += (s, e) => {
                if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_replyBox.Text))
                {
                    ShowNotification("Me", _replyBox.Text, 3, null); 
                    _replyBox.Clear();
                }
            };

            Grid.SetColumn(_replyBox, 0);
            inputGrid.Children.Add(_replyBox);
            _inputContainer.Child = inputGrid;

            mainGrid.Children.Add(_notificationPanel);
            mainGrid.Children.Add(_inputContainer);
            this.Content = mainGrid;
        }

        private void PositionWindow()
        {
            this.Left = 20;
            this.Top = 20;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            try
            {
                // Aggressive Windows Styles
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE);

                // This function is only in Win10 2004+. Wrap to prevent crash on older systems.
                try { SetWindowDisplayAffinity(hwnd, 0x11); } catch { }
            }
            catch { }

            RegisterHotKey(hwnd, HOTKEY_ID_INTERACT, MOD_SHIFT, VK_OEM_3);
            RegisterHotKey(hwnd, HOTKEY_ID_BORDERLESS, MOD_CONTROL | MOD_SHIFT, 0x42); 

            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
            Task.Run(() => ScanForGames());
        }


        private void OnClosed(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID_INTERACT);
            UnregisterHotKey(hwnd, HOTKEY_ID_BORDERLESS);
        }

        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY)
            {
                if ((int)msg.wParam == HOTKEY_ID_INTERACT) { ToggleInteraction(); handled = true; }
                else if ((int)msg.wParam == HOTKEY_ID_BORDERLESS) { ForceForegroundWindowBorderless(); handled = true; }
            }
        }

        private void ScanForGames()
        {
            // If we have a valid game window, don't waste CPU scanning
            if (_gameHwnd != IntPtr.Zero && IsWindow(_gameHwnd)) return;

            string[] targets = { "Minecraft", "Lunar Client", "Badlion Client" };
            string[] exclusions = { "Launcher", "Browser", "Chrome", "Firefox", "Edge", "Setup", "Settings", "Discord" };

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd)) return true;

                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (string.IsNullOrEmpty(title)) return true;

                // 1. Check exclusions
                foreach (var exc in exclusions)
                    if (title.Contains(exc, StringComparison.OrdinalIgnoreCase)) return true;

                // 2. Check targets
                foreach (var target in targets)
                {
                    if (title.Contains(target, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_fixedWindows.Contains(hwnd)) return true;

                        bool hasVersion = title.Contains("1.") || title.Contains("2.");
                        bool isSpecificGame = (target != title.Trim());

                        if (hasVersion || isSpecificGame)
                        {
                            Application.Current.Dispatcher.Invoke(() => ApplyBorderlessFix(hwnd, title));
                            return false; // Stop enumerating
                        }
                    }
                }

                return true;
            }, IntPtr.Zero);
        }

        private void ForceForegroundWindowBorderless()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == new WindowInteropHelper(this).Handle) return;
            ApplyBorderlessFix(hwnd, "Manual Target");
        }

        private void ApplyBorderlessFix(IntPtr hwnd, string sourceName)
        {
            if (!IsWindow(hwnd)) return;
            
            _gameHwnd = hwnd;
            _fixedWindows.Add(hwnd);

            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            int style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE | WS_MAXIMIZE | WS_SYSMENU | WS_POPUP | WS_BORDER);
            SetWindowLong(hwnd, GWL_STYLE, style);

            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, screenWidth, screenHeight, SWP_FRAMECHANGED | SWP_SHOWWINDOW);

            ForceToFront();
            ShowNotification("System", $"Auto-Fixed: {sourceName}", 4, null);
        }

        private void ToggleInteraction()
        {
            _isInteractive = !_isInteractive;
            var hwnd = new WindowInteropHelper(this).Handle;

            if (_isInteractive)
            {
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                _inputContainer.Visibility = Visibility.Visible;
                this.Activate();
                _replyBox.Focus();
                this.Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            }
            else
            {
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                _inputContainer.Visibility = Visibility.Collapsed;
                this.Background = Brushes.Transparent;
            }
        }

        public void ShowNotification(string title, string message, int durationSec, string avatarUrl)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_playSound)
                {
                    try { SystemSounds.Asterisk.Play(); } catch { }
                }

                var notif = CreateNotificationControl(title, message, avatarUrl);
                _notificationPanel.Children.Add(notif);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSec) };
                timer.Tick += (s, e) => { _notificationPanel.Children.Remove(notif); timer.Stop(); };
                timer.Start();
            });
        }

        private Border CreateNotificationControl(string title, string message, string avatarUrl)
        {
            var border = new Border
            {
                Background = _cardBackground, CornerRadius = new CornerRadius(8), Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10), Width = 300, HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = _borderColor, BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarBorder = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = _accentColor, Margin = new Thickness(0, 0, 10, 0) };
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                try {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri(avatarUrl); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit();
                    avatarBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                } catch { }
            }
            else
            {
                avatarBorder.Child = new TextBlock { Text = title.Substring(0, 1).ToUpper(), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            }

            Grid.SetColumn(avatarBorder, 0); grid.Children.Add(avatarBorder);
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock { Text = title, Foreground = _primaryText, FontWeight = FontWeights.Bold, FontSize = 14, FontFamily = new FontFamily("Segoe UI") });
            textStack.Children.Add(new TextBlock { Text = message, Foreground = _secondaryText, FontSize = 13, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI") });
            Grid.SetColumn(textStack, 1); grid.Children.Add(textStack);
            border.Child = grid;
            return border;
        }

        private void ForceToFront()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (_gameHwnd != IntPtr.Zero && IsWindow(_gameHwnd))
            {
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                // Reset target if game closed
                if (_gameHwnd != IntPtr.Zero) _gameHwnd = IntPtr.Zero;
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        // P/Invoke
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_BORDER = 0x00800000;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZE = 0x20000000;
        private const int WS_MAXIMIZE = 0x01000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_OEM_3 = 0xC0;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    }
}
