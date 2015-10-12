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
        private const string SPI_CONTROLLER_NAME = "SPI0";  /* For Raspberry Pi 2, use SPI0                             */
        private const Int32 SPI_CHIP_SELECT_LINE = 0;       /* Line 0 maps to physical pin number 24 on the Rpi2        */
        private SpiDevice SpiMatrix;
        private static readonly byte REG_NOOP =  0x0;
        private static readonly byte REG_DIGI0 =  0x1;
        private static readonly byte REG_DIGI1 =  0x2;
        private static readonly byte REG_DIGI2 =  0x3;
        private static readonly byte REG_DIGI3 =  0x4;
        private static readonly byte REG_DIGI4 =  0x5;
        private static readonly byte REG_DIGI5 =  0x6;
        private static readonly byte REG_DIGI6 =  0x7;
        private static readonly byte REG_DIGI7 =  0x8;
        private static readonly byte REG_DECODEMODE = 0x9;
        private static readonly byte REG_INTENSITY = 0xA;
        private static readonly byte REG_SCANLIMIT = 0xB;
        private static readonly byte REG_SHUTDOWN = 0xC;
        private static readonly byte REG_DISPLAYTEST = 0xF;
        private bool scrollright = false;

        private byte[,] buffer = new byte[8,8];

        public MainPage()
        {
            this.InitializeComponent();
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
            InitAll();
           
        }

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

        private async Task InitSpi()
        {
            try
            {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 2000000;
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

        private async void startScrollRight()
        {
            scrollright = true;
            while (scrollright)
            {
                await System.Threading.Tasks.Task.Delay(20);
                shiftBufferRightLooping();
                writeBuffer();
                bufferToGrid();
            }
        }
        private void sendCommand(SpiDevice dev, byte register, byte data)
        {
            dev.Write(new byte[] { register, data });
            
        }

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

        private void writeRow(int r)
        {
            string s = "";
            for (int i = 0; i < 8; i++)
            {
                s = s + buffer[r-1, i].ToString();
            }   
            sendCommand(SpiMatrix, Convert.ToByte(r), Convert.ToByte(s,2));
        }

        private void writeBuffer()
        {
            for (int i = 1; i < 9; i++) writeRow(i);
        }

        private void setPixel(int r, int c, bool b)
        {
            if(b) buffer[r - 1, c - 1] = Convert.ToByte(1);
            else buffer[r - 1, c - 1] = Convert.ToByte(0);
        }

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

        private void grdMatrix_pointer_pressed(object sender, PointerRoutedEventArgs e)
        {
            Rectangle _rect = sender as Rectangle;
            int _row = (int)_rect.GetValue(Grid.RowProperty);
            int _col = (int)_rect.GetValue(Grid.ColumnProperty);
            togglePixelandRedraw(_row, _col);
        }
    }
}
