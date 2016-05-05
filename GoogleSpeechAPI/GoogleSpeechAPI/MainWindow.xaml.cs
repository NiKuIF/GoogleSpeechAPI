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

using System.Web.Script.Serialization;

namespace GoogleSpeechAPI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class Person
        {
            public string transcript { get; set; }
            public float confidence { get; set; }
        }

        public class Results
        {
            public string result { get; set; }
            public float confidence { get; set; }
        }

        private const string folder_name = "SpeechFiles";
        private MMDevice device;
        private IWaveIn waveIn = null;
        private WaveFileWriter writer;
        private string outputFilename;
        private string outputFilenameFlac;
        private string outputFolder;
       
        public MainWindow()
        {
            InitializeComponent();
            initInputDevices();
            initWaveInDevice();
        }

        #region ButtonEvents

        private void btn_startRecording_Click(object sender, RoutedEventArgs e)
        {
            startRecordingClick();
        }

        private void btn_stopRecording_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }

        private void btn_sendFlacFile_Click(object sender, RoutedEventArgs e)
        {
            sendFlacFile();
        }

        #endregion

        private void initInputDevices()
        {     
            var deviceEnum = new MMDeviceEnumerator();
            var devices = deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
            
            foreach (var dev in devices)
            {
                cb_mics.Items.Add(dev);
                dev.AudioEndpointVolume.Mute = false; // turn on microphone
            }

            if (devices.Count > 0)
            {
                cb_mics.SelectedIndex = 0; // default select first microphone
                device = (MMDevice)cb_mics.SelectedItem;
            }  
        }

        private void startRecordingClick()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }

            if(waveIn != null)
            {
                waveIn.Dispose();
                waveIn = null;
                initWaveInDevice();
            }

            // save recorded file in tmpFolder
            outputFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), folder_name);
            Directory.CreateDirectory(outputFolder);
            Console.WriteLine("Path: " + outputFolder);

            outputFilenameFlac = String.Format("capture_{0:yyy-MM-dd HH-mm-ss}", DateTime.Now);
            outputFilename = outputFilenameFlac + ".wav";
            outputFilenameFlac += ".flac";

            writer = new WaveFileWriter(System.IO.Path.Combine(outputFolder, outputFilename), waveIn.WaveFormat);
            waveIn.StartRecording();
        }

        private void initWaveInDevice()
        {
            // create new WaveIn Device to capture microphone input
            waveIn = new WaveIn();
            waveIn.WaveFormat = new WaveFormat(16000, 1);
      
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
        }

        void OnDataAvailable(object sender, WaveInEventArgs e)
        {      
            if (writer == null)
                return;
            
            writer.Write(e.Buffer, 0, e.BytesRecorded);

            int secondsRecorded = (int)(writer.Length / writer.WaveFormat.AverageBytesPerSecond);
            if (secondsRecorded >= 5)
            {
                StopRecording();
            }
        }

        void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (writer == null)
                return;
            writer.Dispose();
            writer = null;
        }

        // when we stop recording, over an event or something else
        void StopRecording()
        {

            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }


            while (!File.Exists(System.IO.Path.Combine(outputFolder, outputFilenameFlac)))
            {
                convertToFlac();
                Console.WriteLine("convert file");
            }
            
            sendFlacFile();
        }

        private void convertToFlac()
        {
            // wav-File Stream
            FileStream sourceStream = new FileStream(System.IO.Path.Combine(outputFolder, outputFilename), FileMode.Open);
            WAVReader audioSource = new WAVReader(System.IO.Path.Combine(outputFolder, outputFilename), sourceStream);


            AudioBuffer buff = new AudioBuffer(audioSource, 0x10000);
            FlakeWriter flakeWriter = new FlakeWriter(System.IO.Path.Combine(outputFolder, outputFilenameFlac), audioSource.PCM);

            flakeWriter.CompressionLevel = 8;
            while (audioSource.Read(buff, -1) != 0)
            {
                flakeWriter.Write(buff);
            }

            sourceStream.Close();
            flakeWriter.Close();
            audioSource.Close();
        }

        void sendFlacFile()
        {
            FileStream fileStream = File.OpenRead(System.IO.Path.Combine(outputFolder, outputFilenameFlac));
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

                //var serializer = new JavaScriptSerializer();
                //var deserializedResult = serializer.Deserialize<List<Person>>(result);

                if (result.StartsWith("{\"result\":[]}\n{\"result\":[{\"alternative\":"))
                {
                    result = result.Substring("{\"result\":[]}\n{\"result\":[{\"alternative\":".Length);
                }

                if (result.EndsWith(",\"final\":true}],\"result_index\":0}\n"))
                {
                    result = result.Substring(0, result.Length - ",\"final\":true}],\"result_index\":0}\n".Length);
                }


                var serializer = new JavaScriptSerializer();
                var deserializedResult = serializer.Deserialize<List<Person>>(result);

                txt_result.Text = deserializedResult[0].transcript;

                Console.WriteLine("testte");

                /*

            "{\"result\":[]}\n
            {\"result\":[{\"alternative\":[{\"transcript\":\"hello Mark how are you\",\"confidence\":0.96041793},{\"transcript\":\"hello Mark are you\"},{\"transcript\":\"hello Mark how are youuu\"},{\"transcript\":\"hello Mark how are ya\"},{\"transcript\":\"hello Mark how are young\"}],\"final\":true}],\"result_index\":0}\n"



            [
                {"transcript":"hello Mark how are you","confidence":0.98267901},
                {"transcript":"hello Mark how are U"},
                {"transcript":"hello Mark how are youuu"},
                {"transcript":"hello Mark how are you I"},
                {"transcript":"hello Mark how are your"}
            ]

            {\"result\":[]}{\"result\":[{\"alternative\":

                    {"result":[]}
                    {"result":[
                        {"alternative":[
                            {"transcript":"hello Mark how are you","confidence":0.88823956},
                            {"transcript":"hello Mark"},
                            {"transcript":"hello Mark I"},
                            {"transcript":"hello Mark I am"},
                            {"transcript":"hello Mark Ohio"}],
                        "final":true}],
                    "result_index":0}

                 */

            }
        }
    }
}
