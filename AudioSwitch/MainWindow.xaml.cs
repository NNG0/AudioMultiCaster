using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Devices;
using Windows.Media.Render;
using NAudio.CoreAudioApi;
using NAudio.Wave;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AudioSwitch
{
    public sealed partial class MainWindow : Window
    {
        private Dictionary<string, MMDevice> AudioDeviceDict { get; set; } 

        private Dictionary<string,string> NameIDDict { get; set; }

        private Dictionary<string, WasapiOut> SelectedDevices { get; set; }

        private WasapiLoopbackCapture captureDevice;
        private BufferedWaveProvider captureBuffer;

        public MainWindow()
        {
            this.InitializeComponent();
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 1000));
            AppWindow.Move(new Windows.Graphics.PointInt32(0, 0));
            AudioDeviceDict = new Dictionary<string, MMDevice>();
            SelectedDevices = new Dictionary<string, WasapiOut>();
            NameIDDict = new Dictionary<string, string>();
            ListAudioDevices();
            //TODO: get Name
            AudioDeviceListView.ItemsSource = AudioDeviceDict.Values.Select(device => device.FriendlyName);
            //AudioCaptureHandler();

        }

        private void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            ListAudioDevices();
        }

        private void ListAudioDevices()
        {
            AudioDeviceDict.Clear();
            NameIDDict.Clear();
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in devices)
            {
                AudioDeviceDict.Add(device.ID, device);
                NameIDDict.Add(device.FriendlyName, device.ID);
            }
        }

        public void OnStartClicked(object sender, RoutedEventArgs e)
        {
            StartAudioRouting();
        }
        public void OnStopClicked(object sender, RoutedEventArgs e) { StopAudioRouting(); }

        private void StartAudioRouting() 
        {
            captureDevice = new WasapiLoopbackCapture();
            captureBuffer = new BufferedWaveProvider(captureDevice.WaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(10), // Increase the buffer duration
                DiscardOnBufferOverflow = true
            }; 

            captureDevice.DataAvailable += (s, e) =>
            {
                if (captureBuffer.BufferedDuration.TotalMilliseconds < 5000) // Only add if buffer is less than 5 seconds
                {
                    captureBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                }
                else
                {
                    Debug.WriteLine("Buffer full, discarding samples");
                }
            };

            captureDevice.StartRecording();
            foreach (var device in SelectedDevices.Values) {
                device.Init(captureBuffer);
                device.Play();
            }
        }

        private void StopAudioRouting()
        {
            if (captureDevice != null)
            {
                captureDevice.StopRecording();
                captureDevice.Dispose();
                captureDevice = null;
            }

            foreach (var device in SelectedDevices.Values)
            {
                device.Stop();
                device.Dispose();
            }
            SelectedDevices.Clear();
        }

        private void ADVchangeHandler(object sender, SelectionChangedEventArgs e)
        {
            foreach (var item in e.AddedItems)
            {
                if (NameIDDict.TryGetValue(item.ToString(), out string id) &&
                AudioDeviceDict.TryGetValue(id, out var device)) 
                {
                    
                    Debug.WriteLine(device);

                    var wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
                    SelectedDevices.Add(device.ID, wasapiOut);
                    
                    if (captureDevice != null && captureDevice != null)
                    {
                        wasapiOut.Init(captureBuffer);
                        wasapiOut.Play();
                    }
                    
                    Debug.WriteLine(item);
                    Debug.WriteLine(device);
                }             
            }
            
            foreach (var item in e.RemovedItems)
            {
                if (
                    NameIDDict.TryGetValue(item.ToString(), out string id) &&
                    AudioDeviceDict.TryGetValue(id, out var device) &&
                    SelectedDevices.TryGetValue(device.ID, out var wasapiOut)
                    ) 
                {
                    wasapiOut.Stop();
                    wasapiOut.Dispose();
                    SelectedDevices.Remove(device.ID);

                    Debug.WriteLine(item);
                    Debug.WriteLine(device);
                }
                

                
            }
        }
    }
}
