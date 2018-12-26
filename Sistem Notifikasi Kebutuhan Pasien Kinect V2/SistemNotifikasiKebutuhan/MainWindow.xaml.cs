using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;

namespace KinectCoordinateMapping
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor _sensor;
        MultiSourceFrameReader _reader;
        IList<Body> _bodies;
        Floor _floor;

        CameraMode _mode = CameraMode.Color;

        // Client (Controller ini) menggunakan UDP Client untuk menerima respon melalui port 11000
        UdpClient Client = new UdpClient(11000);

        public MainWindow()
        {
            InitializeComponent();

            // Menerima paket UDP (accepted command) dari server secara async
            try
            {
                Client.BeginReceive(new AsyncCallback(recv), null);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {
                _sensor.Open();

                _reader = _sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared | FrameSourceTypes.Body);
                _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }

            if (_sensor != null)
            {
                _sensor.Close();
            }

        }
        // --------- Pengujian Performansi Controller -------------
        DateTime startCommand_time;
        // --------------------------------------------------------

        // penggunaan ushort artinya untuk menghandle apabila user sudah memberi perintah
        // selama kurang lebih 36 menit maka perintah tersebut akan dicoba kirim kembali
        static ushort isCommand = 0; // untuk mengecek jumlah perintah telah dideteksi
        static bool isCommandCalled = false; // untuk mengecek apakah perintah sudah di eksekusi sebelumnya
        string play = ""; // menampung dan menyuarakan perintah yang akan dikirimkan
        string temp_play = ""; // menampung nilai play sebelumnya untuk menghitung berapa kali perintah yang sama diajukan agar sesuai jumlah isCommand
        string temp_test = "-";
        int countCommand = 0; // nantinya akan bernilai 1 untuk menyesuaikan awalnya dengan isCommand (kadang selisih 1 di awal)

        static bool isFall = false; // untuk mendeteksi jatuh. True = Jatuh, False = Bukan Jatuh
        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            //==============Debugging Perintah yang Benar-Benar Dikehendaki============
            temp_play = play;
            Console.WriteLine(
            " IC : " + isCommand +
            "\n ICC : " + isCommandCalled +
            "\n CountCommand : " + countCommand +
            "\n Play : " + play + "\n");
            //=========================================================================

            if (isCommand == 10) // untuk filter perintah awal yang diterima dan juga apabila kedua tangan sudah diatas badan, namun user memberikan perintah yang bermacam-macam
            {
                if (isCommandCalled.Equals(false) && play != "" && countCommand == isCommand) // ketika perintah belum terpanggil sama sekali dan jumlah perintahnya sama pada setiap isCommand frame
                {
                    Task task = new Task(() =>
                    {
                        SendUDP("127.0.0.1", 41181, temp_test, temp_test.Length);
                        speakCommand(play);
                    });
                    task.Start();

                    //===============================Debugging Command dan Performansi===============================
                    Console.WriteLine("*****Perintah dieksekusi!!!*****\n CountCommand : " + countCommand + "\n");

                    TimeSpan delay = DateTime.Now - startCommand_time; // Memperoleh selisih antara waktu terdeteksinya perintah hingga tereksekusinya perintah tersebut
                    Console.WriteLine("################ " + delay + " ################");
                    //===============================================================================================
                }
                else
                {
                    // untuk handle ketika tangan sudah diatas namun belum memberi perintah yang pasti, ketika hendak memberi perintah yang pasti IC == countCommand, play != "", 
                    // dan saat tangan diatas tersebut belum pernah memberikan perintah yang pasti sekali pun, maka akan di reset, agar dapat memberi kesempatan perintah yang pastinya
                    isCommandCalled = false;
                    isCommand = 0;
                    countCommand = 0;
                    return;
                }
                isCommandCalled = true;
                countCommand++;
                isCommand++; //untuk handle ketika return dan keluar dari method event,
                // kinect start-up event acquire frame lagi terus biasaya isCommandnya tetep jadi 1 sedangkan countCommand 0, sehingga akan terpanggil 2x dalam rentang milisecond (diibaratkan sama dengan memberi perintah yang sama 2x, namun yang terpanggil 1x, itu pun setelah di reset)
                // misal : setelah hitungan command "Panggilan Darurat" mencapai 60, dia akan reset kembali sehingga perintah sesudahnya lah yang akan dieksekusi
            }
            else if (isCommand == ushort.MaxValue - 1 || isCommand < ushort.MinValue) // Jika selama 36 menit tangan user memberikan perintah namun tak ada satu pun perintah yang dieksekusi, maka akan di reset
            {
                isCommand = 0;
            }
            //=================================================

            var reference = e.FrameReference.AcquireFrame();

            // Color
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    if (_mode == CameraMode.Color)
                    {
                        camera.Source = frame.ToBitmap();
                    }
                }
            }

            // Depth
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    if (_mode == CameraMode.Depth)
                    {
                        camera.Source = frame.ToBitmap();
                    }
                }
            }

            // Infrared
            using (var frame = reference.InfraredFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    if (_mode == CameraMode.Infrared)
                    {
                        camera.Source = frame.ToBitmap();
                    }
                }
            }

            // Body
            using (var frame = reference.BodyFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    canvas.Children.Clear();

                    _bodies = new Body[frame.BodyFrameSource.BodyCount];

                    frame.GetAndRefreshBodyData(_bodies);

                    // Mendeteksi bidang lantai sebagai acuan dalam deteksi jatuh
                    _floor = new Floor(frame.FloorClipPlane);

                    foreach (var body in _bodies) // pada sistem ini tidak menangani perintah dari user yang banyak, meskipun skeleton mereka dapat terdeteksi hingga 6 orang
                    {
                        if (body.IsTracked)
                        {
                            // Find the Joints

                            //Spine Shoulder
                            Joint spinShoulder = body.Joints[JointType.SpineShoulder];

                            // Right Hand
                            Joint handRight = body.Joints[JointType.HandRight];
                            Joint thumbRight = body.Joints[JointType.ThumbRight];

                            // Left Hand
                            Joint handLeft = body.Joints[JointType.HandLeft];
                            Joint thumbLeft = body.Joints[JointType.ThumbLeft];

                            // Draw hands and thumbs
                            canvas.DrawHand(handRight, _sensor.CoordinateMapper);
                            canvas.DrawThumb(thumbRight, _sensor.CoordinateMapper);
                            canvas.DrawHand(handLeft, _sensor.CoordinateMapper);
                            canvas.DrawThumb(thumbLeft, _sensor.CoordinateMapper);

                            // Find the hand states
                            string rightHandState = "-";
                            string leftHandState = "-";
                            string test = "-";
                            play = "";

                            // Menampung posisi titik pusat massa tubuh (tulang ekor = spinbase)
                            CameraSpacePoint spinBase = body.Joints[JointType.SpineBase].Position;

                            // mengoutputkan jarak antara body joint (SpinBase) ke sensor kinect V2
                            var body_distance = MeasureBodyDistance(spinBase);
                            BodyDistanceValue.Text = body_distance.ToString() + " m";

                            // menampung jarak antara skeleton point dengan lantai
                            double floor_distance = 1; // diinisialisasi dengan nilai 1, karena jika 0 mengindikasikan bahwa posisi user sedang berhimpitan dengan lantai

                            // Mengukur jarak antara titik skeleton spinBase dengan bidang lantai
                            if (_floor != null)
                            {
                                floor_distance = _floor.DistanceFrom(spinBase);
                                FallDetectionValue.Text = floor_distance.ToString() + " m";
                            }

                            // Mendeteksi jatuh ketika jarak antara skeleton point dengan lantai < 0.2
                            if (floor_distance < 0.2 && floor_distance >= 0)
                            {
                                FallDetectionValue.Text = "Fall Detected!";
                                test = "Pasien Jatuh";
                                play = "../../public/sounds/WAV/sound-fall-detection.wav";
                            }
                            // Perintah dapat diberikan ketika tangan kiri diatas, dan tangan kanan dibawah
                            else if (handLeft.Position.Y >= spinShoulder.Position.Y & handRight.Position.Y < spinShoulder.Position.Y & body.HandLeftState != HandState.Unknown)
                            {
                                switch (body.HandLeftState)
                                {
                                    case HandState.Open:
                                        leftHandState = "Open";
                                        break;
                                    case HandState.Closed:
                                        leftHandState = "Closed";
                                        break;
                                    case HandState.Lasso:
                                        leftHandState = "Lasso";
                                        break;
                                    case HandState.Unknown:
                                        leftHandState = "Please, Put your hand";
                                        break;
                                    case HandState.NotTracked:
                                        leftHandState = "Not tracked";
                                        break;
                                    default:
                                        break;
                                }

                                //command Panggilan Darurat dengan TANGAN KIRI
                                switch (body.HandLeftState)
                                {
                                    case HandState.Open:
                                        test = "Panggilan Darurat";
                                        play = "../../public/sounds/WAV/sound-panggilan-darurat.wav";
                                        break;
                                }

                                //command Bantu Buang Hajat dengan TANGAN KIRI
                                switch (body.HandLeftState)
                                {
                                    case HandState.Closed:
                                        test = "Bantu Buang Hajat";
                                        play = "../../public/sounds/WAV/sound-bantu-buang-hajat.wav";
                                        break;
                                }

                                //command Infus Habis dengan TANGAN KIRI
                                switch (body.HandLeftState)
                                {
                                    case HandState.Lasso:
                                        test = "Infus Habis";
                                        play = "../../public/sounds/WAV/sound-infus-habis.wav";
                                        break;
                                }
                            }
                            // Perintah dapat diberikan ketika tangan kanan diatas, dan tangan kiri dibawah
                            else if (handRight.Position.Y >= spinShoulder.Position.Y & handLeft.Position.Y < spinShoulder.Position.Y & body.HandRightState != HandState.Unknown)
                            {
                                switch (body.HandRightState)
                                {
                                    case HandState.Open:
                                        rightHandState = "Open";
                                        break;
                                    case HandState.Closed:
                                        rightHandState = "Closed";
                                        break;
                                    case HandState.Lasso:
                                        rightHandState = "Lasso";
                                        break;
                                    case HandState.Unknown:
                                        rightHandState = "Please, Put your hand";
                                        break;
                                    case HandState.NotTracked:
                                        rightHandState = "Not Tracked";
                                        break;
                                    default:
                                        break;
                                }

                                //command Panggilan Darurat dengan TANGAN KANAN
                                switch (body.HandRightState)
                                {
                                    case HandState.Open:
                                        test = "Panggilan Darurat";
                                        play = "../../public/sounds/WAV/sound-panggilan-darurat.wav";
                                        break;
                                }

                                //command Bantu Buang Hajat dengan TANGAN KANAN
                                switch (body.HandRightState)
                                {
                                    case HandState.Closed:
                                        test = "Bantu Buang Hajat";
                                        play = "../../public/sounds/WAV/sound-bantu-buang-hajat.wav";
                                        break;
                                }

                                //command Infus Habis dengan TANGAN KANAN
                                switch (body.HandRightState)
                                {
                                    case HandState.Lasso:
                                        test = "Infus Habis";
                                        play = "../../public/sounds/WAV/sound-infus-habis.wav";
                                        break;
                                }
                            }

                            // Mengecek apabila user memberikan perintah
                            if (test != "-")
                            {
                                countCommand = (temp_play == play & temp_play != "") ? countCommand + 1 : 1;
                                if (countCommand == 1) startCommand_time = DateTime.Now; // memperoleh waktu awal ketika perintah terdeteksi
                                testCount.Text = countCommand.ToString() + " x";

                                RightStatHand.Text = rightHandState;
                                LeftStatHand.Text = leftHandState;
                                command.Text = test;
                                temp_test = test;
                                isCommand++;
                                return;
                            }

                            RightStatHand.Text = rightHandState;
                            LeftStatHand.Text = leftHandState;
                            command.Text = test;

                            if (handRight.Position.Y < spinShoulder.Position.Y & handLeft.Position.Y < spinShoulder.Position.Y) // untuk menghindari gerakan yang tidak terdefinisi (unknown) oleh heuristic recognition sensor
                            {
                                // set seperti state awal (kedua tangan dibawah spinShoulder) -> sehingga dapat memberikan perintah kembali
                                countCommand = 0;
                                isCommand = 0;
                                isCommandCalled = false;
                            }

                            // COORDINATE MAPPING - Memvisualkan skeleton seseorang yang terdeteksi pada canvas
                            foreach (Joint joint in body.Joints.Values)
                            {
                                if (joint.TrackingState == TrackingState.Tracked)
                                {
                                    Status.Text = "Tracked";
                                    // 3D space point
                                    CameraSpacePoint jointPosition = joint.Position;

                                    // 2D space point
                                    Point point = new Point();

                                    if (_mode == CameraMode.Color)
                                    {
                                        ColorSpacePoint colorPoint = _sensor.CoordinateMapper.MapCameraPointToColorSpace(jointPosition);

                                        point.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
                                        point.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;
                                    }
                                    else if (_mode == CameraMode.Depth || _mode == CameraMode.Infrared) // Change the Image and Canvas dimensions to 512x424
                                    {
                                        DepthSpacePoint depthPoint = _sensor.CoordinateMapper.MapCameraPointToDepthSpace(jointPosition);

                                        point.X = float.IsInfinity(depthPoint.X) ? 0 : depthPoint.X;
                                        point.Y = float.IsInfinity(depthPoint.Y) ? 0 : depthPoint.Y;
                                    }

                                    // Draw
                                    Ellipse ellipse = new Ellipse

                                    {
                                        Fill = Brushes.LawnGreen,
                                        Width = 20,
                                        Height = 20
                                    };

                                    Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
                                    Canvas.SetTop(ellipse, point.Y - ellipse.Height / 2);

                                    canvas.Children.Add(ellipse);
                                }
                                else
                                {
                                    Status.Text = "Not Tracked";
                                }
                            }
                        }
                    }
                }
            }
        }
        enum CameraMode
        {
            Color,
            Depth,
            Infrared
        }
        void speakCommand(String pathSound)
        {
            System.Media.SoundPlayer speak = new System.Media.SoundPlayer();
            speak.SoundLocation = pathSound;
            speak.PlaySync();
        }
        private void SendUDP(string hostNameOrAddress, int destinationPort, string data, int count)
        {
            IPAddress destination = Dns.GetHostAddresses(hostNameOrAddress)[0];
            IPEndPoint endPoint = new IPEndPoint(destination, destinationPort);
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SendTo(buffer, endPoint);
            socket.Close();
            System.Console.WriteLine("Sent : " + data);
        }

        private void recv(IAsyncResult res) // CallBack method untuk menerima UDP packet dari server
        {
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 41181);
            byte[] received = Client.EndReceive(res, ref RemoteIpEndPoint);

            string feedbackCommand = Encoding.UTF8.GetString(received);
            string playFeedback = "";
            if (feedbackCommand == "Panggilan Darurat" | feedbackCommand == "Infus Habis" | feedbackCommand == "Bantu Buang Hajat" | feedbackCommand == "Pasien Jatuh")
            {
                playFeedback = "../../public/sounds/WAV/sound-perintah-diterima.wav";
            }
            else if (feedbackCommand == "Sibuk")
            {
                playFeedback = "../../public/sounds/WAV/sound-perawat-sibuk.wav";
            }
            else
            {
                playFeedback = "../../public/sounds/WAV/sound-perintah-tidak-dikenali.wav";
            }

            Task taskFeedBack = new Task(() => speakCommand(playFeedback));
            taskFeedBack.Start();
            Client.BeginReceive(new AsyncCallback(recv), null);
        }

        public double MeasureBodyDistance(CameraSpacePoint point)
        {
            // rumus panjang vektor
            return Math.Sqrt(
                point.X * point.X +
                point.Y * point.Y +
                point.Z * point.Z
            );
        }
        public string lefttHandState { get; set; }
    }
}
