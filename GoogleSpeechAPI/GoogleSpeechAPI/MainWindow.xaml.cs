using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// used for microphone and Speech sending
using System.Net;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

using CUETools.Codecs;
using CUETools.Codecs.FLAKE;


namespace GoogleSpeechAPI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private MMDevice device;
        private IWaveIn waveIn;
        private WaveFileWriter writer;
        private string outputFilename;
        private string outputFolder;

        private string filename;

        public MainWindow()
        {
            InitializeComponent();
            listAllInputDevices();
        }

        private void btn_startRecording_Click(object sender, RoutedEventArgs e)
        {
            startRecordingClick();
        }

        private void convertToFlac()// Stream sourceStream, Stream destinationStream)
        {
            // wav-File Stream
            var sourceStream = new FileStream(System.IO.Path.Combine(outputFolder, outputFilename), FileMode.Open);
            //var destinationStream = new FileStream(System.IO.Path.Combine(outputFolder, "myFlacTest.flac"), FileMode.Open);


            var audioSource = new WAVReader(null, sourceStream);
            try
            {
              /*  if (audioSource.PCM.SampleRate != 16000)
                {
                    throw new InvalidOperationException("Incorrect frequency - WAV file must be at 16 KHz.");
                }*/

                var buff = new AudioBuffer(audioSource, 0x10000);
                // var flakeWriter = new FlakeWriter(null, destinationStream, audioSource.PCM);
                var flakeWriter = new FlakeWriter(System.IO.Path.Combine(outputFolder, "myFlacTest.flac"), audioSource.PCM);

                flakeWriter.CompressionLevel = 8;
                while (audioSource.Read(buff, -1) != 0)
                {
                    flakeWriter.Write(buff);
                }
                flakeWriter.Close();
                //destinationStream.Close();
            }
            finally
            {
                audioSource.Close();
            }
        }    


    private void listAllInputDevices()
        {
            var deviceEnum = new MMDeviceEnumerator();
            var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            foreach (var dev in devices)
            {
                Console.WriteLine(dev.DeviceFriendlyName);
                dev.AudioEndpointVolume.Mute = false; // turn on microphone
                device = dev; // I have only on microphone device
            }
        }

        private void startRecordingClick()
        {
            outputFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NAudioDemo");
            Directory.CreateDirectory(outputFolder);
            Console.WriteLine("Path: " + outputFolder);

            waveIn = CreateWaveInDevice();
            outputFilename = String.Format("NAudioDemo {0:yyy-MM-dd HH-mm-ss}.wav", DateTime.Now);
            writer = new WaveFileWriter(System.IO.Path.Combine(outputFolder, outputFilename), waveIn.WaveFormat);
            waveIn.StartRecording();
        }

        private IWaveIn CreateWaveInDevice()
        {
            // create new WaveIn Device to capture microphone input

            IWaveIn newWaveIn;

            // use WaveIn, mybe try WaceInEvent
            newWaveIn = new WaveIn();
            newWaveIn.WaveFormat = new WaveFormat(16000, 1);//(8000, 1);

            newWaveIn.DataAvailable += OnDataAvailable;
            newWaveIn.RecordingStopped += OnRecordingStopped;
            return newWaveIn;
        }

        void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            Console.WriteLine("Data Available");
            if (writer == null)
                return;

            writer.Write(e.Buffer, 0, e.BytesRecorded);
            int secondsRecorded = (int)(writer.Length / writer.WaveFormat.AverageBytesPerSecond);
            if (secondsRecorded >= 3)
            {
                StopRecording();
            }
        }

        void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            writer.Dispose();
            writer = null;
        }

        // when we stop recording, over an event or something else
        void StopRecording()
        {
            if (writer == null)
                return;

            Console.WriteLine("StopRecording");
            waveIn.StopRecording();
            writer.Dispose();
            writer = null;
            convertToFlac();
        }

        void sendFlacFile()
        {
            FileStream fileStream = File.OpenRead("C:\\Users\\niskurt-win10\\AppData\\Local\\Temp\\NAudioDemo\\myFlacTest.flac");
            MemoryStream memoryStream = new MemoryStream();
            memoryStream.SetLength(fileStream.Length);
            fileStream.Read(memoryStream.GetBuffer(), 0, (int)fileStream.Length);
            Console.WriteLine("FileStream Length: " + (int)fileStream.Length);

            byte[] BA_AudioFile = memoryStream.GetBuffer();
            HttpWebRequest _HWR_SpeechToText = null;
            _HWR_SpeechToText =
                        (HttpWebRequest)HttpWebRequest.Create(
                            "https://www.google.com/speech-api/v2/recognize?output=json&lang=en-us&key=AIzaSyBcLgcy7SIGfKwaVyuhtv1Z7Hf40K87GvM");
            _HWR_SpeechToText.Credentials = CredentialCache.DefaultCredentials;
            _HWR_SpeechToText.Method = "POST";
            _HWR_SpeechToText.ContentType = "audio/x-flac; rate=16000";
            _HWR_SpeechToText.ContentLength = BA_AudioFile.Length;
            Stream stream = _HWR_SpeechToText.GetRequestStream();
            stream.Write(BA_AudioFile, 0, BA_AudioFile.Length);
            stream.Close();

            HttpWebResponse HWR_Response = (HttpWebResponse)_HWR_SpeechToText.GetResponse();
            if (HWR_Response.StatusCode == HttpStatusCode.OK)
            {
                StreamReader SR_Response = new StreamReader(HWR_Response.GetResponseStream());

                string result = SR_Response.ReadToEnd();

                txt_result.Text = result;
                Console.WriteLine(result);
            }
        }

        private void btn_sendFlacFile_Click(object sender, RoutedEventArgs e)
        {
            sendFlacFile();
        }
    }
}
