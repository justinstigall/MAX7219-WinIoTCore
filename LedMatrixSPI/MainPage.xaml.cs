using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using System.Threading.Tasks;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LedMatrixSPI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string SPI_CONTROLLER_NAME = "SPI0";  // For Raspberry Pi 2, use SPI0
        private const Int32 SPI_CHIP_SELECT_LINE = 0;       // Line 0 maps to physical pin number 24 on the Rpi2

        // Specify all of the Registers available on the MAX2719
        private static readonly byte REG_NOOP =  0x0; // unused
        private static readonly byte REG_DIGI0 =  0x1; // Row 1
        private static readonly byte REG_DIGI1 =  0x2; // Row 2
        private static readonly byte REG_DIGI2 =  0x3; // Row 3
        private static readonly byte REG_DIGI3 =  0x4; // Row 4
        private static readonly byte REG_DIGI4 =  0x5; // Row 5
        private static readonly byte REG_DIGI5 =  0x6; // Row 6
        private static readonly byte REG_DIGI6 =  0x7; // Row 7
        private static readonly byte REG_DIGI7 =  0x8; // Row 8
        private static readonly byte REG_DECODEMODE = 0x9; // Set to 0 for 8x8 Matrix
        private static readonly byte REG_INTENSITY = 0xA; // Brightness (1-16)
        private static readonly byte REG_SCANLIMIT = 0xB; // Number of rows to scan
        private static readonly byte REG_SHUTDOWN = 0xC; // Device shutdown mode (0 - off, 1 - on)
        private static readonly byte REG_DISPLAYTEST = 0xF; // All LEDS on/off test (0 - off, 1 - on)

        private SpiDevice SpiMatrix; // Intialize SpiMatrix Variable

        private byte[,] buffer = new byte[8,8]; // Create 8 by 8 matrix to hold 64 bytes representing LED off (0) and on (1)

        private bool scrollright = false; // Variable to controll scrolling on/off

        private int scrollSpeed = 20;

        public MainPage()
        {
            this.InitializeComponent();
            CreateXAMLMatrix();
            InitAll();
           
        }

        // Create an 8x8 Matrix UI that can turn LEDs on and off
        private void CreateXAMLMatrix()
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    Rectangle r = new Rectangle();
                    r.Name = string.Format("rect{0}{1}", i, j);
                    r.SetValue(Grid.RowProperty, i);
                    r.SetValue(Grid.ColumnProperty, j);
                    r.Fill = new SolidColorBrush(Windows.UI.Colors.Black);
                    r.PointerPressed += new PointerEventHandler(grdMatrix_pointer_pressed);
                    grdMatrix.Children.Add(r);
                }
            }
        }

        // On clicking of one of the Matrix LEDs find which row/column and enable/disable
        private void grdMatrix_pointer_pressed(object sender, PointerRoutedEventArgs e)
        {
            Rectangle _rect = sender as Rectangle;
            int _row = (int)_rect.GetValue(Grid.RowProperty);
            int _col = (int)_rect.GetValue(Grid.ColumnProperty);
            togglePixelandRedraw(_row, _col);
        }

        // Initialize SPI device and start Matrix scrolling
        private async void InitAll()
        {
            try
            {
                await InitSpi();
                InitMatrix();
            }
            catch
            {
                return;
            }
        }

        // Initialize SPI Device
        private async Task InitSpi()
        {
            try
            {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 2000000; // 2 MHz
                settings.Mode = SpiMode.Mode0; 
                string spiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
                var devicesInfo = await DeviceInformation.FindAllAsync(spiAqs);
                SpiMatrix = await SpiDevice.FromIdAsync(devicesInfo[0].Id, settings);

            }
            /* If initialization fails, display the exception and stop running */
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }

        // Itialize MAX2719 Chip and
        // Put in sample LED pattern and start scrolling
        private async void InitMatrix()
        {
            sendCommand(SpiMatrix, REG_SCANLIMIT, 7);
            sendCommand(SpiMatrix, REG_DECODEMODE, 0);
            sendCommand(SpiMatrix, REG_DISPLAYTEST, 0);
            sendCommand(SpiMatrix, REG_SHUTDOWN, 1);
            sendCommand(SpiMatrix, REG_INTENSITY, 5 );
            clearBuffer();
            setPixel(1, 1, true);
            setPixel(2, 2, true);
            setPixel(3, 3, true);
            setPixel(4, 4, true);
            setPixel(5, 5, true);
            setPixel(6, 6, true);
            setPixel(7, 7, true);
            setPixel(8, 8, true);
            writeBuffer();
            scrollright = true;
            startScrollRight();

            
        }

        // Start scrolling the LEDs Right one pixel at a time
        private async void startScrollRight()
        {
            scrollright = true;
            while (scrollright)
            {
                await System.Threading.Tasks.Task.Delay(scrollSpeed);
                shiftBufferRightLooping();
                writeBuffer();
                bufferToGrid();
            }
        }
        
        // Wrapper to send SPI commands two bytes at a time (register -> data)
        private void sendCommand(SpiDevice dev, byte register, byte data)
        {
            dev.Write(new byte[] { register, data });
            
        }

        // Set all LEDs in buffer to 0
        private void clearBuffer()
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    buffer[i, j] = 0;
                }
            }
        }

        // Update a row of information in the LED Matrix
        private void writeRow(int r)
        {
            string s = "";
            for (int i = 0; i < 8; i++)
            {
                s = s + buffer[r-1, i].ToString();
            }   
            sendCommand(SpiMatrix, Convert.ToByte(r), Convert.ToByte(s,2));
        }

        // Write the entire buffer to the LED Matrix by row
        private void writeBuffer()
        {
            for (int i = 1; i < 9; i++) writeRow(i);
        }

        // Set a single pixel in the buffer
        private void setPixel(int r, int c, bool b)
        {
            if(b) buffer[r - 1, c - 1] = Convert.ToByte(1);
            else buffer[r - 1, c - 1] = Convert.ToByte(0);
        }

        // Toggle a single pixel and force a redraw
        private void togglePixelandRedraw(int r, int c)
        {
            if (buffer[r,c] == Convert.ToByte(1))
            {
                buffer[r, c] = 0;
            }
            else
            {
                buffer[r, c] = 1;
            }
            writeBuffer();
            bufferToGrid();
        }

        // Shift the buffer of data to the right dropping pixles that fall off display
        private void shiftBufferRight()
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[i, 0] = 0;
                for (int j = 7; j > 0; j--)
                {
                    buffer[i, j] = buffer[i, j - 1];
                }
            }
        }

        // Shift the buffer to the right, looping data that falls off the right side to the left side
        private void shiftBufferRightLooping()
        {
            for (int i = 0; i < 8; i++)
            {
                byte saved = buffer[i,7];
                for (int j = 7; j > 0; j--)
                {
                    buffer[i, j] = buffer[i, j - 1];
                }
                buffer[i, 0] = saved;
            }
        }

        // Write out the buffer as a string
        private string bufferToString()
        {
            string s = "";
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    s = s + buffer[i, j].ToString() + " ";
                }
                s = s + '\n';
            }
            return s;
        }

        // Write out the buffer to the UI
        private void bufferToGrid()
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    string id = "rect" + i.ToString() + j.ToString();
                    Rectangle r = (Rectangle)FindName(id);
                    if (buffer[i,j] == 1)
                    {
                        r.Fill = new SolidColorBrush(Windows.UI.Colors.Red);
                    }
                    else
                    {
                        r.Fill = new SolidColorBrush(Windows.UI.Colors.Black);
                    }
                }
            }
        }

        // Button click callback to start or stop scrolling
        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (scrollright)
            {
                scrollright = false;
            }
            else
            {
                startScrollRight();
            }
        }

        private void slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            scrollSpeed = (int)slider.Value;
        }
    }
}
