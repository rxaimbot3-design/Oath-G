using System;
using System.Drawing;
using System.Windows.Forms;
using AshtronV7.Models;
using AshtronV7.Services;
using AshtronV7.Views;

namespace AshtronV7.Services
{
    public class TrayService : IDisposable
    {
        private readonly EngineService _engineService;
        private readonly LoggerService _logger;
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private MainWindow _mainWindow;

        public TrayService(EngineService engineService, LoggerService logger)
        {
            _engineService = engineService;
            _logger = logger;
            InitializeTray();
        }

        private void InitializeTray()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.RenderMode = ToolStripRenderMode.Professional;

            var openItem = new ToolStripMenuItem("Open Dashboard", null, (s, e) => ShowMainWindow());
            openItem.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _contextMenu.Items.Add(openItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var engineStatusItem = new ToolStripMenuItem("Engine: Stopped") { Enabled = false, Name = "EngineStatus" };
            _contextMenu.Items.Add(engineStatusItem);

            var startEngineItem = new ToolStripMenuItem("Start Competitive Engine", null, async (s, e) => await StartEngineAsync());
            startEngineItem.Name = "StartEngine";
            _contextMenu.Items.Add(startEngineItem);

            var stopEngineItem = new ToolStripMenuItem("Stop Engine", null, (s, e) => StopEngine());
            stopEngineItem.Name = "StopEngine";
            stopEngineItem.Enabled = false;
            _contextMenu.Items.Add(stopEngineItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var restoreItem = new ToolStripMenuItem("Restore Windows", null, (s, e) => RestoreWindows());
            _contextMenu.Items.Add(restoreItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());
            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "ASHTRON V7 — ULTRA FRAME ENGINE",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();

            _engineService.OnEngineStateChanged += UpdateEngineState;
            _engineService.OnEngineEvent += (msg) => UpdateTooltip(msg);
        }

        private Icon CreateTrayIcon()
        {
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(Color.FromArgb(0, 200, 255));
                g.FillEllipse(brush, 4, 4, 24, 24);
                using var font = new Font("Segoe UI", 14, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.Black);
                g.DrawString("A7", font, textBrush, 4, 6);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null || !_mainWindow.IsLoaded)
            {
                _mainWindow = new MainWindow(_engineService, _logger);
                _mainWindow.Closed += (s, e) => _mainWindow = null;
            }
            _mainWindow.Show();
            _mainWindow.WindowState = System.Windows.WindowState.Normal;
            _mainWindow.Activate();
        }

        public void UpdateEngineState(EngineState state)
        {
            if (_contextMenu.InvokeRequired)
            {
                _contextMenu.Invoke(new Action(() => UpdateEngineState(state)));
                return;
            }

            var statusItem = _contextMenu.Items["EngineStatus"] as ToolStripMenuItem;
            var startItem = _contextMenu.Items["StartEngine"] as ToolStripMenuItem;
            var stopItem = _contextMenu.Items["StopEngine"] as ToolStripMenuItem;

            if (statusItem != null)
            {
                statusItem.Text = $"Engine: {state}";
            }

            if (startItem != null)
            {
                startItem.Enabled = state == EngineState.Stopped || state == EngineState.Error;
            }

            if (stopItem != null)
            {
                stopItem.Enabled = state == EngineState.Active || state == EngineState.Monitoring;
            }

            _notifyIcon.Icon = CreateTrayIconForState(state);
        }

        private Icon CreateTrayIconForState(EngineState state)
        {
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                Color color = state switch
                {
                    EngineState.Active => Color.FromArgb(0, 200, 100),
                    EngineState.Starting => Color.FromArgb(255, 180, 0),
                    EngineState.Error => Color.FromArgb(255, 80, 80),
                    _ => Color.FromArgb(100, 100, 100)
                };
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, 4, 4, 24, 24);
                using var font = new Font("Segoe UI", 14, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.Black);
                g.DrawString("A7", font, textBrush, 4, 6);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void UpdateTooltip(string message)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = $"ASHTRON V7 — {message}";
                if (_notifyIcon.Text.Length > 63)
                    _notifyIcon.Text = _notifyIcon.Text.Substring(0, 60) + "...";
            }
        }

        private async Task StartEngineAsync()
        {
            await _engineService.StartEngineAsync();
        }

        private void StopEngine()
        {
            _engineService.StopEngine();
        }

        private void RestoreWindows()
        {
            _engineService.RestoreWindows();
        }

        private void ExitApplication()
        {
            _engineService.StopEngine();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            System.Windows.Application.Current?.Shutdown();
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
    }
}