using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Windows.Forms; 
using Microsoft.Win32;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Reflection;

namespace NatifyOverlay
{
    public class Program
    {
        private static DiscordSocketClient? _client;
        private static OverlayWindow? _overlay;
        private static Config? _config;
        private static NotifyIcon? _trayIcon;
        private const string AppName = "NatifyOverlay";
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "natify_log.txt");

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
            
            try
            {
                Log("Starting NatifyOverlay...");
                StartApp();
            }
            catch (Exception ex)
            {
                string error = $"FATAL ERROR ON STARTUP:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                Log(error);
                System.Windows.Forms.MessageBox.Show(error, "NatifyOverlay Crash Reporter");
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static void StartApp()
        {
            var app = new System.Windows.Application();
            Log("WPF Application initialized.");

            LoadConfig();
            Log("Config loaded.");

            _overlay = new OverlayWindow();
            Log("Overlay window created.");

            // Set App Icon from Embedded Resource (Safer method)
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "NatifyOverlay.IMG_20260114_070206_012.jpg";
                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        _overlay.Icon = decoder.Frames[0];
                        Log("Overlay icon set from resource.");
                    }
                    else
                    {
                        Log($"Warning: Resource '{resourceName}' not found. Available resources: " + string.Join(", ", assembly.GetManifestResourceNames()));
                    }
                }
            }
            catch (Exception ex) { Log($"Failed to load overlay icon: {ex.Message}"); }

            CreateTrayIcon();
            Log("Tray icon created.");

            UpdateStartupRegistry(_config!.RunAtStartup);
            
            Task.Run(() => StartDiscordBot());
            Log("Discord bot task started.");

            Log("Running WPF Application...");
            app.Run(_overlay);
        }

        private static void CreateTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "NatifyOverlay (Running)"
            };

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "NatifyOverlay.IMG_20260114_070206_012.jpg";
                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var bmp = new Bitmap(stream))
                        {
                            _trayIcon.Icon = Icon.FromHandle(bmp.GetHicon());
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                Log($"Failed to load tray icon: {ex.Message}");
                _trayIcon.Icon = SystemIcons.Application; 
            }

            var contextMenu = new ContextMenuStrip();
            
            contextMenu.Items.Add("Test Notification", null, (s, e) => 
            {
                _overlay?.ShowNotification("[Test Server] Test User", "This is a test notification!", _config!.DurationSeconds, null);
            });

            var startupItem = new ToolStripMenuItem("Run at Startup") { Checked = _config!.RunAtStartup };
            startupItem.Click += (s, e) => 
            {
                _config.RunAtStartup = !_config.RunAtStartup;
                startupItem.Checked = _config.RunAtStartup;
                UpdateStartupRegistry(_config.RunAtStartup);
                SaveConfig();
            };
            contextMenu.Items.Add(startupItem);

            contextMenu.Items.Add("Controls", null, (s, e) =>
            {
                System.Windows.Forms.MessageBox.Show("1. Press Shift + ~ (Tilde) to toggle Interactive Mode.\n" +
                                                     "   (Interactive Mode allows you to click and reply)\n\n" +
                                                     "2. Press Ctrl + Shift + B to force the active game into Borderless Windowed mode.\n" +
                                                     "   (Use this if the overlay is hidden behind the game)", 
                                                     "NatifyOverlay Controls");
            });

            contextMenu.Items.Add("-"); 
            contextMenu.Items.Add("Exit", null, (s, e) => 
            {
                if (_trayIcon != null) _trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
                Environment.Exit(0);
            });

            _trayIcon.ContextMenuStrip = contextMenu;
        }

        private static void UpdateStartupRegistry(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(AppName, $"\"{Process.GetCurrentProcess().MainModule?.FileName}\"");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Registry Error: " + ex.Message);
            }
        }

        private static void LoadConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (!File.Exists(configPath))
            {
                _config = new Config();
                SaveConfig();
                Log("Created new config.json.");
                System.Windows.Forms.MessageBox.Show("config.json created. Please add your Bot Token and restart.");
                Environment.Exit(0);
            }
            _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath))!;
        }

        private static void SaveConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            File.WriteAllText(configPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }

        private static async Task StartDiscordBot()
        {
            try
            {
                var config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent };
                _client = new DiscordSocketClient(config);

                _client.MessageReceived += async (msg) =>
                {
                    if (msg.Author.IsBot) return;

                    bool isChannelAllowed = _config!.AllowedChannelIds.Count == 0 || _config.AllowedChannelIds.Contains(msg.Channel.Id);
                    bool isUserAllowed = _config.AllowedUserIds.Count == 0 || _config.AllowedUserIds.Contains(msg.Author.Id);

                    if (isChannelAllowed && isUserAllowed)
                    {
                        string server = (msg.Channel as SocketGuildChannel)?.Guild.Name ?? "DM";
                        string title = $"[{server}] {msg.Author.Username}";
                        string avatarUrl = msg.Author.GetAvatarUrl() ?? msg.Author.GetDefaultAvatarUrl();
                        _overlay?.ShowNotification(title, msg.CleanContent, _config.DurationSeconds, avatarUrl);
                    }
                };

                await _client.LoginAsync(TokenType.Bot, _config!.BotToken);
                await _client.StartAsync();
                Log("Discord bot logged in and started.");
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Log("Error starting Discord Bot: " + ex.Message);
            }
        }
    }

    public class Config
    {
        public string BotToken { get; set; } = "YOUR_TOKEN_HERE";
        public System.Collections.Generic.List<ulong> AllowedChannelIds { get; set; } = new();
        public System.Collections.Generic.List<ulong> AllowedUserIds { get; set; } = new();
        public int DurationSeconds { get; set; } = 5;
        public bool RunAtStartup { get; set; } = false;
    }
}
