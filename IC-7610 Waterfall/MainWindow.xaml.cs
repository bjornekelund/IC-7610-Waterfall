using System;
using System.Windows;
using System.Windows.Input;
using System.IO.Ports;

namespace IC_7610_Waterfall
{
    public partial class MainWindow : Window
    {
        // Settings
        public static string ComPort = "COM3";
        public static byte TrxAddress = 0x98;
        public static byte EdgeSet = 3; // which scope edge should be manipulated
        public static float DefaultOffset = 1; // default is dx 1kHz from left edge of waterfall for USB CW
        public static float DefaultWidth = 5; // default is 6kHz wide waterfall
        public static int ResponseTime = 100; // time in milliseconds for the radio to respond

        // Statics
        public static byte[] CIVRequestFrequency = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x25, 0x00, 0xFD };
        public static byte[] CIVRequestMode = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x26, 0x00, 0xFD };

        public static byte[] CIVSetFixedMode = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x14, 0x0, 0x1, 0xFD };
        public static byte[] CIVSetEdgeSet = new byte[] { 0xFE, 0xFE, TrxAddress, 0xE0, 0x27, 0x16, 0x0, EdgeSet, 0xFD };

        public static int[,,] ScopeEdge = new int[,,] {
            {{1810, 1845}, {1840, 1860}, {1840, 2000}, {1800, 2000}}, // CW, Digital, Phone, Band
			{{3500, 3580}, {3580, 3600}, {3600, 3800}, {3500, 3800}}, // BandIndex, modeindex, lower/upper
			{{5352, 5366}, {5352, 5366}, {5352, 5366}, {5352, 5366}},
            {{7000, 7040}, {7040, 7080}, {7040, 7200}, {7000, 7200}},
            {{10100, 10140}, {10130, 10150}, {10100, 10150}, {10100, 10150}},
            {{14000, 14080}, {14070, 14100}, {14100, 14350}, {14000, 14350}},
            {{18068, 18109}, {18089, 18109}, {18111, 18168}, {18068, 18168}},
            {{21000, 21080}, {21080, 21150}, {21150, 21450}, {21000, 21450}},
            {{24890, 24920}, {24910, 24932}, {24931, 24990}, {24890, 24990}},
            {{28000, 28100}, {28080, 28110}, {28300, 28600}, {28000, 29000}},
            {{50000, 50100}, {50300, 50350}, {50100, 50500}, {50000, 50500}},
            {{70000, 71000}, {70000, 71000}, {70000, 71000}, {70000, 71000}}};

        public static string[,] ModeName = new string[,] { // RadioMode, RadioDigital 
            { "LSB", "LSB-D" },
            { "USB", "USB-D" },
            { "AM", "AM-D" },
            { "CW", "03-D?" },
            { "RTTY", "04-D?" },
            { "FM", "FM-D" },
            { "06?", "06-D?" },
            { "CW-R", "07-D?" },
            { "RTTY-R", "08-D?" },
            { "09?", "09-D?" },
            { "10?", "10-D?" },
            { "11?", "11-D?" },
            { "PSK", "12-D?" },
            { "PSK-R", "13-D?" }};


        // mapping of ham bands to radio waterfall segments 
        static int[] EdgeSegment = new int[] { 2, 3, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13 }; // 160-4m


        static float[] ModeOffsetDefault = new float[] { };
        static float[] ModeWidthDefault = new float[] { };

        // Global variables        
        public static byte[] ReadBuffer = new byte[100]; // Larger than ever needed
        public static int RadioMode; // Radio mode
        public static int RadioDigital; // Radio digital flag
        public static float RadioFrequency; // Radio frequency
        public static string DisplayRadioMode;
        public static int ModeIndex;
        public static int BandIndex;

        SerialPort port = new SerialPort(ComPort, 19200, Parity.None, 8, StopBits.One);

        public MainWindow()
        {
            InitializeComponent();
            try {
                port.Open();
            }
            catch {
                MessageBoxResult result = MessageBox.Show("Could not open serial port " + ComPort, "IC-7610 Waterfall", MessageBoxButton.OK, MessageBoxImage.Question);
                if (result == MessageBoxResult.OK) {
                    Application.Current.Shutdown();
                }
            }

            QueryRadio();
        }

        private void ModeClick(object sender, RoutedEventArgs e) {
            QueryRadio();

            SetupRadio(ScopeEdge[BandIndex, ModeIndex, 0], ScopeEdge[BandIndex, ModeIndex, 1]);
        }

        private void PileupClick(object sender, RoutedEventArgs e) {
            QueryRadio();
            SetupRadio((int)(RadioFrequency + 0.5 - Int32.Parse(WaterfallOffset.Text)),
                (int)(RadioFrequency + 0.5 - Int32.Parse(WaterfallOffset.Text)) + Int32.Parse(WaterfallWidth.Text));
        }

        private void EntryKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return)
            {
                QueryRadio();
                SetupRadio((int)(RadioFrequency + 0.5 - Int32.Parse(WaterfallOffset.Text)),
                    (int)(RadioFrequency + 0.5 - Int32.Parse(WaterfallOffset.Text)) + Int32.Parse(WaterfallWidth.Text));
            }
        }

        void QueryRadio() {
            port.Write(CIVRequestFrequency, 0, CIVRequestFrequency.Length); // Request frequency
            System.Threading.Thread.Sleep(ResponseTime); // Wait
            port.Read(ReadBuffer, 0, port.BytesToRead); // Read response including echo

            RadioFrequency = 0.01f * (float)(1E8 * ((ReadBuffer[17] & 0xf0) >> 4) + 1E7 * (ReadBuffer[17] & 0x0f) +
                1E6 * ((ReadBuffer[16] & 0xf0) >> 4) + 1E5 * (ReadBuffer[16] & 0x0f) + 1E4 * ((ReadBuffer[15] & 0xf0) >> 4) +
                1E3 * (ReadBuffer[15] & 0x0f) + 1E2 * ((ReadBuffer[14] & 0xf0) >> 4) +
                1E1 * (ReadBuffer[14] & 0x0f) + 1 * ((ReadBuffer[13] & 0xf0) >> 4));

            port.Write(CIVRequestMode, 0, CIVRequestMode.Length); // Request Mode
            System.Threading.Thread.Sleep(ResponseTime); // Wait
            port.Read(ReadBuffer, 0, port.BytesToRead); // Read response including echo
            RadioMode = 10 * ((ReadBuffer[13] & 0xf0) >> 4) + ReadBuffer[13] & 0x0f;
            if ((ReadBuffer[14] & 0x03) != 0) { RadioDigital = 1; } else { RadioDigital = 0; }

            if (RadioDigital != 0 | RadioMode == 4 | RadioMode == 8 | RadioMode == 12 | RadioMode == 13) { // RTTY/PSK/Digital
                ModeIndex = 1; // Digital
            } else if (RadioMode == 3 | RadioMode == 7) { // CW or CW-R
                ModeIndex = 0; // CW
            } else {
                ModeIndex = 2; // Phone or other
            }

            BandIndex = 0; // identify sub band 
            while (RadioFrequency >= ScopeEdge[BandIndex, 3, 1]) {
                BandIndex += 1;
            }

            DisplayDxQRG.Content = RadioFrequency.ToString("N2") + "kHz";
            DisplayMode.Content = ModeName[RadioMode, RadioDigital];
            // RadioMode, RadioFrequency, BandIndex, ModeIndex are now set
        }


        void SetupRadio(int lower_edge, int upper_edge) {
            byte[] CIVSetEdges = new byte[19] 
            {
                0xFE, 0xFE, TrxAddress, 0xE0,
                0x27, 0x1E,
                (byte)((EdgeSegment[BandIndex] / 10) * 16 + (EdgeSegment[BandIndex] % 10)),
                EdgeSet,
                0x00, // Lower 10Hz & 1Hz
                (byte)((lower_edge % 10) * 16 + 0), // 1kHz & 100Hz
                (byte)(((lower_edge / 100) % 10) * 16 + ((lower_edge / 10) % 10)), // 100kHz & 10kHz
                (byte)(((lower_edge / 10000) % 10) * 16 + (lower_edge / 1000) % 10), // 10MHz & 1MHz
                0x00, // 1GHz & 100MHz
                0x00, // // Upper 10Hz & 1Hz 
                (byte)((upper_edge % 10) * 16 + 0), // 1kHz & 100Hz
                (byte)(((upper_edge / 100) % 10) * 16 + (upper_edge / 10) % 10), // 100kHz & 10kHz
                (byte)(((upper_edge / 10000) % 10) * 16 + (upper_edge / 1000) % 10), // 10MHz & 1MHz
                0x00, // 1GHz & 100MHz
                0xFD
            };

            DisplayWaterfallEdges.Content = lower_edge.ToString("N0") + " - " + upper_edge.ToString("N0") + "kHz";

            port.Write(CIVSetFixedMode, 0, CIVSetFixedMode.Length); // Set fixed mode
            System.Threading.Thread.Sleep(ResponseTime); // Wait
            port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

            port.Write(CIVSetEdgeSet, 0, CIVSetEdgeSet.Length); // set edge set EdgeSet
            System.Threading.Thread.Sleep(ResponseTime); // Wait
            port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo

            port.Write(CIVSetEdges, 0, CIVSetEdges.Length); // set edge set EdgeSet
            System.Threading.Thread.Sleep(ResponseTime); // Wait
            port.Read(ReadBuffer, 0, port.BytesToRead); // Flush response including echo
        }
    }
}