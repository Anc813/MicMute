using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Win32;
using Shortcut;
using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reactive;
using System.Windows.Forms;


namespace MicMute
{
    public partial class MainForm : Form
    {
        public CoreAudioController AudioController = new CoreAudioController();
        private readonly HotkeyBinder hotkeyBinder = new HotkeyBinder();
        private readonly RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MicMute");
        private readonly string registryKeyName = "Hotkey";

        private Hotkey hotkey;
        enum MicStatus
        {
            Initial, On, Off, Error
        }
        private MicStatus currentStatus;

        private bool myVisible; 
        public bool MyVisible
        {
            get { return myVisible; }
            set { myVisible = value; Visible = value; }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private void OnNextDevice(DeviceChangedArgs next)
        {
            UpdateDevice(AudioController.DefaultCaptureDevice);
        }

        private void MyHide()
        {
            ShowInTaskbar = false;
            Location = new Point(-10000, -10000);
            MyVisible = false;
        }

        private void MyShow()
        {
            MyVisible = true;
            ShowInTaskbar = true;
            CenterToScreen();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            MyHide();
            UpdateDevice(AudioController.DefaultCaptureDevice);
            AudioController.AudioDeviceChanged.Subscribe(OnNextDevice);

            var hotkeyValue = registryKey.GetValue(registryKeyName);
            if (hotkeyValue != null)
            {
                var converter = new Shortcut.Forms.HotkeyConverter();
                hotkey = (Hotkey)converter.ConvertFromString(hotkeyValue.ToString());
                hotkeyBinder.Bind(hotkey).To(ToggleMicStatus);
            }

            //AudioController.AudioDeviceChanged.Subscribe(x =>
            //{
            //    Debug.WriteLine("{0} - {1}", x.Device.Name, x.ChangedType.ToString());
            //});
        }

        private void OnMuteChanged(DeviceMuteChangedArgs next)
        {
            UpdateStatus(next.Device);
        }

        IDisposable muteChangedSubscription;
        public void UpdateDevice(IDevice device)
        {
            muteChangedSubscription?.Dispose();
            muteChangedSubscription = device?.MuteChanged.Subscribe(OnMuteChanged);
            UpdateStatus(device);
        }

        Icon iconOff = Properties.Resources.off;
        Icon iconOn = Properties.Resources.on;
        Icon iconError = Properties.Resources.error;

        public void PlaySound(string relativePath)
        {
            string path = Path.Combine(Application.StartupPath, relativePath);
            if (File.Exists(path))
            {
                SoundPlayer simpleSound = new SoundPlayer(path);
                simpleSound.Play();
            }
        }

        public void UpdateStatus(IDevice device)
        {
            MicStatus newStatus = (device != null) ? (device.IsMuted ? MicStatus.Off : MicStatus.On) : MicStatus.Error;
            if (currentStatus != newStatus)
            {
                currentStatus = newStatus;
                switch (currentStatus)
                {
                    case MicStatus.On:
                        UpdateIcon(iconOn, device.FullName);
                        PlaySound("on.wav");
                        break;
                    case MicStatus.Off:
                        UpdateIcon(iconOff, device.FullName);
                        PlaySound("off.wav");
                        break;
                    case MicStatus.Error:
                        UpdateIcon(iconOff, "< No device >");
                        PlaySound("error.wav");
                        break;
                }
            }
        }
        private void UpdateIcon(Icon icon, string tooltipText)
        {
            this.icon.Icon = icon;
            this.icon.Text = tooltipText;
        }

        public async void ToggleMicStatus()
        {
            await AudioController.DefaultCaptureDevice?.ToggleMuteAsync();
        }

        public void UpdateStatus()
        {
            var device = AudioController.DefaultCaptureDevice;

            if (device != null)
            {
                UpdateIcon(device.IsMuted ? Properties.Resources.off : Properties.Resources.on, device.FullName);
            }
            else
            {
                UpdateIcon(Properties.Resources.error, "< No device >");
            }
        }

        private void Icon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ToggleMicStatus();
            }
        }

        private void HotkeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (hotkey != null)
            {
                hotkeyTextBox.Hotkey = hotkey;
                hotkeyBinder.Unbind(hotkey);
            }
            MyShow();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MyVisible)
            {
                MyHide();
                e.Cancel = true;

                hotkey = hotkeyTextBox.Hotkey;

                if (hotkey == null)
                {
                    registryKey.DeleteValue(registryKeyName, false);
                }
                else
                {
                    if (!hotkeyBinder.IsHotkeyAlreadyBound(hotkey))
                    {
                        registryKey.SetValue(registryKeyName, hotkey);
                        hotkeyBinder.Bind(hotkey).To(ToggleMicStatus);
                    }
                }
            }
        }

        private void ButtonReset_Click(object sender, EventArgs e)
        {
            hotkeyTextBox.Hotkey = null;
            hotkeyTextBox.Text = "None";
        }
        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
