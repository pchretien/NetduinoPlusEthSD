using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.IO;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using Microsoft.SPOT.Net.NetworkInformation;

namespace NetduinoPlusEthSD
{
    public class Program
    {
        private bool pin13Value;
        private readonly OutputPort pin13 = new OutputPort(Pins.GPIO_PIN_D13, false);

        private int a0Value;
        private readonly AnalogInput a0 = new AnalogInput(Pins.GPIO_PIN_A0);

        private int a1Value;
        private readonly AnalogInput a1 = new AnalogInput(Pins.GPIO_PIN_A1);
        OutputPort arefSelect = new OutputPort((Cpu.Pin)56, false); // use external aref

        private Timer tempTimer;
        private Timer lightTimer;
        private Timer watchDogTimer;

        private readonly InterruptPort pin2 = new InterruptPort(
            Pins.GPIO_PIN_D2, 
            true,
            Port.ResistorMode.PullUp,
            Port.InterruptMode.InterruptEdgeLow
            );
        
        public static void Main()
        {
            // Create and initialize the program instance
            Program me = new Program();
            me.InitNetwork();

            // Get time  online
            me.SetTime();

            // Start logging ...
            me.AppendToFile(
                    @"\SD\out.log",
                    "Starting application ...");

            // Init the IO pins ...
            me.InitIo();

            // Start timers
            me.InitTimers();

            Thread.Sleep(Timeout.Infinite);
        }

        private void InitNetwork()
        {
            // write your code here
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    if (!networkInterface.IsDhcpEnabled)
                    {
                        // Switch to DHCP ...
                        networkInterface.EnableDhcp();
                        networkInterface.RenewDhcpLease();
                        Thread.Sleep(10000);
                    }

                    Debug.Print("IP Address: " + networkInterface.IPAddress);
                    Debug.Print("Subnet mask " + networkInterface.SubnetMask);
                }
            }
        }

        private void InitTimers()
        {
            TimerCallback watchDogDelegate = new TimerCallback(this.WatchDog);
            watchDogTimer = new Timer(watchDogDelegate, null, 1000, 1000);

            TimerCallback lightCheckDelegate = new TimerCallback(this.CheckLightLevel);
            lightTimer = new Timer(lightCheckDelegate, null, 60000, 60000);

            TimerCallback tempCheckDelegate  = new TimerCallback(this.CheckTemperature);
            tempTimer = new Timer(tempCheckDelegate, null, 60000, 60000);
        }

        private void InitIo()
        {
            a0.SetRange(0, 1024);
            a1.SetRange(0, 1024);
            pin2.OnInterrupt += new NativeEventHandler(this.OnInterrupt);
        }

        private bool VolumeExist()
        {
            VolumeInfo[] volumes = VolumeInfo.GetVolumes();
            foreach (VolumeInfo volumeInfo in volumes)
            {
                if (volumeInfo.Name.Equals("SD"))
                    return true;
            }

            return false;
        }

        private void AppendToFile(
            string filename,
            string message)
        {
            if (!VolumeExist())
                return;

            try
            {
                FileStream file = File.Exists(filename)
                                      ? new FileStream(filename, FileMode.Append)
                                      : new FileStream(filename, FileMode.Create);

                StreamWriter streamWriter = new StreamWriter(file);
                streamWriter.WriteLine(DateTime.Now.ToString() + ": " + message);
                streamWriter.Flush();
                streamWriter.Close();

                file.Close();
            }
            catch(Exception)
            {
            }
        }

        public void WatchDog(Object stateInfo)
        {
            pin13Value = !pin13Value;
            pin13.Write(pin13Value);
        }

        public void CheckLightLevel(Object stateInfo)
        {
            a0Value = a0.Read();

            Debug.Print("Light: " + a0Value);
            AppendToFile(@"\SD\light.log", "Light level " + a0Value);
        }

        public void CheckTemperature(Object stateInfo)
        {
            a1Value = a1.Read();
            double temperature = (a1Value*3500)/1024.0;
            temperature = (temperature - 500)/10.0;

            Debug.Print("Temp: " + temperature);
            AppendToFile(@"\SD\temp.log", "Temperature: " + temperature);
        }

        private void OnInterrupt(uint port, uint state, DateTime time)
        {
            Debug.Print("Pin="+port+" State="+state+" Time"+time);
            pin2.ClearInterrupt();
        }

        private void SetTime()
        {
            DateTime currentTime = NtpClient.GetNetworkTime();
            Utility.SetLocalTime(currentTime);

            AppendToFile(
                    @"\SD\out.log",
                    "SetLocalTime to " + currentTime.ToString());
        }

        private void Blink(int repeat)
        {
            for(int i=0; i<2*repeat; i++)
            {
                pin13Value = !pin13Value;
                pin13.Write(pin13Value);
                Thread.Sleep(100);
            }
        }
    }
}