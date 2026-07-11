using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Shortcut;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;


namespace MicMute
{
    public partial class MainForm : Form
    {
        const string DEFAULT_RECORDING_DEVICE = "Default recording device";

        // NAudio does not run any background peak-metering timer (unlike the previous
        // AudioSwitcher library), so simply enumerating / holding devices does not leak.
        private readonly MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
        private readonly HotkeyBinder hotkeyBinder = new HotkeyBinder();
        private readonly RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MicMute");

        private readonly Dictionary<string, SoundPlayer> soundPlayers = new Dictionary<string, SoundPlayer>();

        private readonly IMMNotificationClient notificationClient;
        private bool notificationClientRegistered;

        // The device we are currently monitoring for mute changes.
        private MMDevice currentDevice;
        private AudioEndpointVolumeNotificationDelegate volumeHandler;

        // toggle
        private readonly string registryKeyName = "Hotkey";
        private Hotkey hotkey;

        // mute
        private readonly string registryKeyMute = "HotkeyMute";
        private Hotkey muteHotkey;

        // unmute
        private readonly string registryKeyUnmute = "HotkeyUnmute";
        private Hotkey unMuteHotkey;

        private readonly string registryDeviceId = "DeviceId";
        private readonly string registryDeviceName = "DeviceName";

        private string selectedDeviceId;
        private string selectedDeviceName;
        private MicSelectorForm micSelectorForm;


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
            notificationClient = new DeviceNotificationClient(this);
        }

        // Marshal a callback that arrives on a WASAPI/system thread back onto the UI thread.
        private void RunOnUi(Action action)
        {
            if (!IsHandleCreated || IsDisposed) return;
            try { BeginInvoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        // Raised by the endpoint notification client on any add/remove/state/default change.
        private void OnDevicesChanged()
        {
            RunOnUi(UpdateSelectedDevice);
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
            selectedDeviceId = (string)registryKey.GetValue(registryDeviceId) ?? "";
            selectedDeviceName = (string)registryKey.GetValue(registryDeviceName) ?? DEFAULT_RECORDING_DEVICE;

            UpdateSelectedDevice();
            enumerator.RegisterEndpointNotificationCallback(notificationClient);
            notificationClientRegistered = true;

            // toggle
            var hotkeyValue = registryKey.GetValue(registryKeyName);
            if (hotkeyValue != null)
            {
                var converter = new Shortcut.Forms.HotkeyConverter();
                hotkey = (Hotkey)converter.ConvertFromString(hotkeyValue.ToString());
                if (!hotkeyBinder.IsHotkeyAlreadyBound(hotkey)) hotkeyBinder.Bind(hotkey).To(ToggleMicStatus);
            }

            // mute
            hotkeyValue = registryKey.GetValue(registryKeyMute);
            if (hotkeyValue != null)
            {
                var converter = new Shortcut.Forms.HotkeyConverter();
                muteHotkey = (Hotkey)converter.ConvertFromString(hotkeyValue.ToString());
                if (!hotkeyBinder.IsHotkeyAlreadyBound(muteHotkey)) hotkeyBinder.Bind(muteHotkey).To(MuteMicStatus);
            }

            // unmute
            hotkeyValue = registryKey.GetValue(registryKeyUnmute);
            if (hotkeyValue != null)
            {
                var converter = new Shortcut.Forms.HotkeyConverter();
                unMuteHotkey = (Hotkey)converter.ConvertFromString(hotkeyValue.ToString());
                if (!hotkeyBinder.IsHotkeyAlreadyBound(unMuteHotkey)) hotkeyBinder.Bind(unMuteHotkey).To(UnMuteMicStatus);
            }
        }

        private void OnMuteChanged(AudioVolumeNotificationData data)
        {
            // Fired on a WASAPI notification thread - refresh the tray icon on the UI thread.
            RunOnUi(() => UpdateStatus(currentDevice));
        }

        public MMDevice GetSelectedDevice()
        {
            try
            {
                if (string.IsNullOrEmpty(selectedDeviceId))
                {
                    return enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)
                        ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)
                        : null;
                }

                MMDevice device = enumerator.GetDevice(selectedDeviceId);
                if (device != null && device.State == DeviceState.Active)
                {
                    return device;
                }

                device?.Dispose();
                return null;
            }
            catch
            {
                // Invalid / stale device id (e.g. saved by an older version) or the device
                // is gone - treat as "no device".
                return null;
            }
        }

        public void UpdateSelectedDevice()
        {
            UpdateDevice(GetSelectedDevice());
        }

        public void UpdateDevice(MMDevice device)
        {
            string newId = device?.ID;
            string currentId = currentDevice?.ID;

            if (newId == currentId)
            {
                // Same endpoint we are already monitoring - drop the freshly fetched
                // duplicate and just refresh the status.
                device?.Dispose();
                UpdateStatus(currentDevice);
                return;
            }

            // Switch to a different device: release the previous one (this also
            // unregisters its COM volume-change callback).
            if (currentDevice != null)
            {
                if (volumeHandler != null)
                {
                    try { currentDevice.AudioEndpointVolume.OnVolumeNotification -= volumeHandler; }
                    catch { }
                }
                currentDevice.Dispose();
                currentDevice = null;
                volumeHandler = null;
            }

            currentDevice = device;
            if (currentDevice != null)
            {
                volumeHandler = OnMuteChanged;
                try { currentDevice.AudioEndpointVolume.OnVolumeNotification += volumeHandler; }
                catch { }
            }

            UpdateStatus(currentDevice);
        }

        Icon iconOff = Properties.Resources.off;
        Icon iconOn = Properties.Resources.on;
        Icon iconError = Properties.Resources.error;

        public void PlaySound(string relativePath)
        {
            if (!soundPlayers.ContainsKey(relativePath))
            {
                string path = Path.Combine(Application.StartupPath, relativePath);
                if (File.Exists(path))
                {
                    soundPlayers[relativePath] = new SoundPlayer(path);
                }
                else
                {
                    soundPlayers[relativePath] = null;
                }
            }

            soundPlayers[relativePath]?.Play();
        }

        private static string SafeDeviceName(MMDevice device)
        {
            try { return device.FriendlyName; }
            catch { return DEFAULT_RECORDING_DEVICE; }
        }

        public void UpdateStatus(MMDevice device)
        {
            MicStatus newStatus;
            string tooltip = "< No device >";

            if (device == null)
            {
                newStatus = MicStatus.Error;
            }
            else
            {
                try
                {
                    newStatus = device.AudioEndpointVolume.Mute ? MicStatus.Off : MicStatus.On;
                    tooltip = SafeDeviceName(device);
                }
                catch
                {
                    newStatus = MicStatus.Error;
                }
            }

            bool playSound = currentStatus != MicStatus.Initial && currentStatus != newStatus;
            currentStatus = newStatus;
            switch (currentStatus)
            {
                case MicStatus.On:
                    UpdateIcon(iconOn, tooltip);
                    if (playSound) PlaySound("on.wav");
                    break;
                case MicStatus.Off:
                    UpdateIcon(iconOff, tooltip);
                    if (playSound) PlaySound("off.wav");
                    break;
                case MicStatus.Error:
                    UpdateIcon(iconError, "< No device >");
                    if (playSound) PlaySound("error.wav");
                    break;
            }
        }
        private void UpdateIcon(Icon icon, string tooltipText)
        {
            this.icon.Icon = icon;
            this.icon.Text = tooltipText.Substring(0, Math.Min(tooltipText.Length, 63));
        }

        // mute == null -> toggle
        private void SetMute(bool? mute)
        {
            MMDevice device = currentDevice;
            bool ownsDevice = false;
            if (device == null)
            {
                device = GetSelectedDevice();
                ownsDevice = true;
            }
            if (device == null) return;

            try
            {
                AudioEndpointVolume volume = device.AudioEndpointVolume;
                volume.Mute = mute ?? !volume.Mute;
            }
            catch { }
            finally
            {
                if (ownsDevice) device.Dispose();
            }
        }

        public void ToggleMicStatus()
        {
            SetMute(null);
        }

        public void MuteMicStatus()
        {
            SetMute(true);
        }

        public void UnMuteMicStatus()
        {
            SetMute(false);
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
            // toggle
            if (hotkey != null)
            {
                hotkeyTextBox.Hotkey = hotkey;
                if (hotkeyBinder.IsHotkeyAlreadyBound(hotkey)) hotkeyBinder.Unbind(hotkey);
            }

            // mute
            if (muteHotkey != null)
            {
                muteTextBox.Hotkey = muteHotkey;
                if (hotkeyBinder.IsHotkeyAlreadyBound(muteHotkey)) hotkeyBinder.Unbind(muteHotkey);
            }

            // unmute
            if (unMuteHotkey != null)
            {
                unmuteTextBox.Hotkey = unMuteHotkey;
                if (hotkeyBinder.IsHotkeyAlreadyBound(unMuteHotkey)) hotkeyBinder.Unbind(unMuteHotkey);
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
                        if (!hotkeyBinder.IsHotkeyAlreadyBound(hotkey)) hotkeyBinder.Bind(hotkey).To(ToggleMicStatus);
                    }
                }

                muteHotkey = muteTextBox.Hotkey;

                if (muteHotkey == null)
                {
                    registryKey.DeleteValue(registryKeyMute, false);
                }
                else
                {
                    if (!hotkeyBinder.IsHotkeyAlreadyBound(muteHotkey))
                    {
                        registryKey.SetValue(registryKeyMute, muteHotkey);
                        if (!hotkeyBinder.IsHotkeyAlreadyBound(muteHotkey)) hotkeyBinder.Bind(muteHotkey).To(MuteMicStatus);
                    }
                }


                unMuteHotkey = unmuteTextBox.Hotkey;

                if (unMuteHotkey == null)
                {
                    registryKey.DeleteValue(registryKeyUnmute, false);
                }
                else
                {
                    if (!hotkeyBinder.IsHotkeyAlreadyBound(unMuteHotkey))
                    {
                        registryKey.SetValue(registryKeyUnmute, unMuteHotkey);
                        if (!hotkeyBinder.IsHotkeyAlreadyBound(unMuteHotkey)) hotkeyBinder.Bind(unMuteHotkey).To(UnMuteMicStatus);
                    }
                }

            }
        }

        private void ButtonReset_Click(object sender, EventArgs e)
        {
            hotkeyTextBox.Hotkey = null;
            hotkeyTextBox.Text = "None";
        }
        private void muteReset_Click(object sender, EventArgs e)
        {
            muteTextBox.Hotkey = null;
            muteTextBox.Text = "None";
        }

        private void unmuteReset_Click(object sender, EventArgs e)
        {
            unmuteTextBox.Hotkey = null;
            unmuteTextBox.Text = "None";
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            micSelectorForm = new MicSelectorForm();
            ComboBox comboBox = micSelectorForm.cbMics;
            comboBox.Items.Clear();

            bool registryExists = false;

            ComboboxItem defaultItem = new ComboboxItem();
            defaultItem.Text = DEFAULT_RECORDING_DEVICE;
            defaultItem.deviceId = "";
            comboBox.Items.Add(defaultItem);

            if (selectedDeviceId == "")
            {
                registryExists = true;
                comboBox.SelectedIndex = comboBox.Items.Count - 1;
            }

            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                ComboboxItem item = new ComboboxItem();
                item.Text = SafeDeviceName(device);
                item.deviceId = device.ID;
                comboBox.Items.Add(item);

                if (item.deviceId == selectedDeviceId)
                {
                    registryExists = true;
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                }

                device.Dispose();
            }

            if (!registryExists) {
                ComboboxItem item = new ComboboxItem();
                item.Text = "(unavailable) " + selectedDeviceName;
                item.deviceId = selectedDeviceId;
                comboBox.Items.Add(item);
                comboBox.SelectedIndex = comboBox.Items.Count - 1;
            }
            DialogResult result = micSelectorForm.ShowDialog();
            Console.WriteLine(result);
            ComboboxItem selectedItem = (ComboboxItem)comboBox.SelectedItem;

            registryKey.SetValue(registryDeviceId, selectedItem.deviceId);
            registryKey.SetValue(registryDeviceName, selectedItem.Text);
            selectedDeviceName = selectedItem.Text;
            selectedDeviceId = selectedItem.deviceId;

            micSelectorForm.Dispose();

            UpdateSelectedDevice();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                // Custom resource disposal
                if (notificationClientRegistered)
                {
                    try { enumerator.UnregisterEndpointNotificationCallback(notificationClient); }
                    catch { }
                    notificationClientRegistered = false;
                }

                if (currentDevice != null)
                {
                    if (volumeHandler != null)
                    {
                        try { currentDevice.AudioEndpointVolume.OnVolumeNotification -= volumeHandler; }
                        catch { }
                    }
                    currentDevice.Dispose();
                    currentDevice = null;
                    volumeHandler = null;
                }

                enumerator?.Dispose();
                registryKey?.Dispose();
                hotkeyBinder?.Dispose();

                foreach (var player in soundPlayers.Values)
                {
                    player?.Dispose();
                }
                soundPlayers.Clear();
            }
            base.Dispose(disposing);
        }

        // Receives device add/remove/state/default-change notifications from Core Audio.
        private class DeviceNotificationClient : IMMNotificationClient
        {
            private readonly MainForm form;

            public DeviceNotificationClient(MainForm form)
            {
                this.form = form;
            }

            public void OnDeviceStateChanged(string deviceId, DeviceState newState) => form.OnDevicesChanged();
            public void OnDeviceAdded(string pwstrDeviceId) => form.OnDevicesChanged();
            public void OnDeviceRemoved(string deviceId) => form.OnDevicesChanged();

            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            {
                if (flow == DataFlow.Capture) form.OnDevicesChanged();
            }

            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
        }
    }
}
