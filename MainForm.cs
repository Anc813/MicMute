﻿using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Win32;
using Shortcut;
using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Reactive;
using System.Windows.Forms;
using System.Timers;


namespace MicMute
{
    public partial class MainForm : Form
    {
        const string DEFAULT_RECORDING_DEVICE = "Default recording device";
        public CoreAudioController AudioController = new CoreAudioController();
        private readonly HotkeyBinder hotkeyBinder = new HotkeyBinder();
        private readonly RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\MicMute");

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

        public bool key_hold = false;

        // Time in milliseconds.
        // Wait and revert mute state after key "release".
        public static int key_hold_toggle_back_timer_wait = 1000;

        private static System.Timers.Timer key_hold_toggle_back_timer;
        public bool key_hold_toggled_back = true;
        public bool key_hold_held = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private void OnNextDevice(DeviceChangedArgs next)
        {
            UpdateSelectedDevice();
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
            AudioController.AudioDeviceChanged.Subscribe(OnNextDevice);

            // toggle
            var hotkeyValue = registryKey.GetValue(registryKeyName);
            if (hotkeyValue != null)
            {
                var converter = new Shortcut.Forms.HotkeyConverter();
                hotkey = (Hotkey)converter.ConvertFromString(hotkeyValue.ToString());
                if (!hotkeyBinder.IsHotkeyAlreadyBound(hotkey)) hotkeyBinder.Bind(hotkey).To(ToggleMicStatusCheck);
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

            //AudioController.AudioDeviceChanged.Subscribe(x =>
            //{
            //    Debug.WriteLine("{0} - {1}", x.Device.Name, x.ChangedType.ToString());
            //});

            // track hold/release option
            key_hold_toggle_back_timer = new System.Timers.Timer(key_hold_toggle_back_timer_wait);
            key_hold_toggle_back_timer.Elapsed += OnToggleTimerDoneAutoToggle;
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
        public IDevice getSelectedDevice()
        {
            return selectedDeviceId == "" ? AudioController.DefaultCaptureDevice : AudioController.GetDevice(new Guid(selectedDeviceId), DeviceState.Active);
        }

        public void UpdateSelectedDevice()
        {
            UpdateDevice(getSelectedDevice());
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
            bool playSound = currentStatus != MicStatus.Initial && currentStatus != newStatus;
            currentStatus = newStatus;
            switch (currentStatus)
            {
                case MicStatus.On:
                    UpdateIcon(iconOn, device.FullName);
                    if (playSound) PlaySound("on.wav");
                    break;
                case MicStatus.Off:
                    UpdateIcon(iconOff, device.FullName);
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
            this.icon.Text = tooltipText;
        }

        public void OnToggleTimerDoneAutoToggle(Object source, ElapsedEventArgs e)
        {
            // Reset.
            key_hold_toggle_back_timer.Stop();
            key_hold_toggled_back = true;

            // Toggle to previous mute state, if key was held.
            if (key_hold_held)
            {
                key_hold_held = false;
                ToggleMicStatus();
            }
        }

        public async void ToggleMicStatus()
        {
            await getSelectedDevice()?.ToggleMuteAsync();
        }

        public void ToggleMicStatusCheck()
        {
            if (key_hold)
            {
                // If state was toggled back, then restart key hold.
                if (key_hold_toggled_back)
                {
                    key_hold_toggled_back = false;
                    key_hold_toggle_back_timer.Start();
                }
                else
                {
                    // Restart toggle back timer.
                    key_hold_held = true;
                    key_hold_toggle_back_timer.Stop();
                    key_hold_toggle_back_timer.Start();
                    return;
                }
            }

            ToggleMicStatus();
        }

        public async void MuteMicStatus()
        {
            await getSelectedDevice()?.SetMuteAsync(true);
        }

        public async void UnMuteMicStatus()
        {
            await getSelectedDevice()?.SetMuteAsync(false);
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
                        if (!hotkeyBinder.IsHotkeyAlreadyBound(hotkey)) hotkeyBinder.Bind(hotkey).To(ToggleMicStatusCheck);
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

            foreach (CoreAudioDevice device in AudioController.GetCaptureDevices())
            {
                if (device.State == DeviceState.Active)
                {
                    ComboboxItem item = new ComboboxItem();
                    item.Text = device.FullName;
                    item.deviceId = device.Id.ToString();
                    comboBox.Items.Add(item);

                    if (item.deviceId == selectedDeviceId)
                    {
                        registryExists = true;
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }

            if (!registryExists) {
                ComboboxItem item = new ComboboxItem();
                item.Text = "(unavailable) " + registryDeviceName.ToString();
                item.deviceId = selectedDeviceId.ToString();
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

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            key_hold = !key_hold;
        }
    }
}
