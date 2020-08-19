using Microsoft.Extensions.Logging.Abstractions;
using SmartGlass;
using SmartGlass.Common;
using SmartGlass.Nano;
using SmartGlass.Nano.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XboxWebApi.Authentication;
using XboxWebApi.Authentication.Model;
using XboxWebApi.Services;
using XboxWebApi.Services.Api;

namespace Xstream
{
    static class Program
    {
        public static string UserHash = null;
        public static string XToken = null;

        public static NanoClient Nano = null;

        public static AudioFormat AudioFormat = null;
        public static VideoFormat VideoFormat = null;
        public static AudioFormat ChatAudioFormat = null;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AllocConsole();

            Console.Write("tokenFilePath: ");
            string tokenFilePath = Console.ReadLine();

            AuthenticationService auth;

            if (!File.Exists(tokenFilePath))
            {
                Shell.WriteLine("Warning: '{0}' file not found.\n", tokenFilePath);

                string reqURL = AuthenticationService.GetWindowsLiveAuthenticationUrl();

                Console.WriteLine("1) Open following URL in your WebBrowser:\n\n{0}\n\n" +
                                        "2) Authenticate with your Microsoft Account\n" +
                                        "3) Paste returned URL from addressbar: \n", reqURL);

                // Call requestUrl via WebWidget or manually and authenticate

                try
                {
                    string url = Console.ReadLine();
                    WindowsLiveResponse rep = AuthenticationService.ParseWindowsLiveResponse(url);
                    auth = new AuthenticationService(rep);

                    auth.Authenticate();
                }
                catch (Exception e)
                {
                    Shell.WriteLine($"Error: Authentication failed, error: {e.Message}");
                    Shell.PressAnyKeyToContinue();
                    return;
                }

                Console.WriteLine(auth.XToken);
                Console.WriteLine(auth.UserInformation);

                // Save token to JSON

                FileStream tokenOutputFile = null;
                try
                {
                    tokenOutputFile = new FileStream(tokenFilePath, FileMode.Create);
                }
                catch (Exception e)
                {
                    Shell.WriteLine("Error: Failed to open token outputfile \'{0}\', error: {1}",
                        tokenOutputFile, e.Message);
                    Shell.PressAnyKeyToContinue();
                    return;
                }
                auth.DumpToFile(tokenOutputFile);
                tokenOutputFile.Close();

                Console.WriteLine("Storing tokens to file \'{0}\' on successful auth",
                        tokenOutputFile.Name);
            }
            else
            {
                // Load token from JSON

                FileStream fs = new FileStream(tokenFilePath, FileMode.Open);
                auth = AuthenticationService.LoadFromFile(fs);
                try
                {
                    auth.Authenticate();
                }
                catch (Exception e)
                {
                    Shell.WriteLine($"Error: Failed to refresh XBL tokens, error: {e.Message}");
                    Shell.PressAnyKeyToContinue();
                    return;
                }
                fs.Close();
            }

            UserHash = auth.XToken.UserInformation.Userhash;
            XToken = auth.XToken.Jwt;

            Discover().Wait();

            Console.Write("Input IP Address or hostname: ");
            string addressOrHostname = Console.ReadLine();

            Console.WriteLine($"Connecting to {addressOrHostname}...");
            SmartGlassClient client;
            try
            {
                Task<SmartGlassClient> connect = SmartGlassClient.ConnectAsync(
                        addressOrHostname, UserHash, XToken);

                // 如果Task失败了GetAwaiter()会直接抛出异常，而Task.Wait()会抛出AggregateException
                client = connect.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                if (e is SmartGlassException)
                    Shell.WriteLine($"Error: Failed to connect: {e.Message}");
                else if (e is TimeoutException)
                    Shell.WriteLine($"Error: Timeout while connecting: {e.Message}");
                else
                    Shell.WriteLine($"Error: {e}");

                Shell.PressAnyKeyToContinue();
                return;
            }

            // Get general gamestream configuration
            GamestreamConfiguration config = GamestreamConfiguration.GetStandardConfig();
            // Modify standard config, if desired
            /*
             * GAME_STREAMING_VERY_HIGH_QUALITY_SETTINGS: 12000000,1080,60,59,0,10,40,170
             * GAME_STREAMING_HIGH_QUALITY_SETTINGS: 8000000,720,60,59,0,10,40,170
             * GAME_STREAMING_MEDIUM_QUALITY_SETTINGS: 6000002,720,60,3600,0,40,70,200
             * GAME_STREAMING_LOW_QUALITY_SETTINGS: 3000001,480,30,3600,0,40,70,200
             * 
             * SETTINGS:
             * Unknown1,Unknown2,Unknown3,Unknown4,Unknown5,Unknown6,Unknown7,Unknown8
             * Unknown1 UrcpMaximumRate
             * Unknown2 VideoMaximumHeight
             * Unknown3 VideoMaximumFrameRate
             * Unknown4 ?
             * Unknown5 AudioBufferLengthHns if 0 use Unknown1
             * Unknown6 AudioSyncMinLatency
             * Unknown7 AudioSyncDesiredLatency
             * Unknown8 AudioSyncMaxLatency
             * 
             * refer to: https://github.com/OpenXbox/xbox-smartglass-nano-python/issues/7
             * default: GAME_STREAMING_LOW_QUALITY_SETTINGS
             */
            config.UrcpMaximumRate = (int)(3.000001 * 1000000);
            config.VideoMaximumHeight = 480;
            config.VideoMaximumFrameRate = 30;
            // config.Unknown4
            config.AudioBufferLengthHns = config.UrcpMaximumRate;
            config.AudioSyncMinLatency = 40;
            config.AudioSyncDesiredLatency = 70;
            config.AudioSyncMaxLatency = 200;

            // 由于小数点向上进位，因此误差 +-1 的情况下，永远满足最小分辨率16:9
            config.VideoMaximumWidth = (int)Math.Ceiling(config.VideoMaximumHeight / 9.0 * 16);

            GamestreamSession session = client.BroadcastChannel.StartGamestreamAsync(config)
                .GetAwaiter().GetResult();
            Console.WriteLine($"Connecting to NANO // TCP: {session.TcpPort}, UDP: {session.UdpPort}");

            Console.WriteLine($"Running protocol init...");
            Nano = new NanoClient(addressOrHostname, session);
            try
            {
                // General Handshaking & Opening channels
                Nano.InitializeProtocolAsync().Wait();

                // Start Controller input channel
                Nano.OpenInputChannelAsync((uint)config.VideoMaximumWidth, (uint)config.VideoMaximumHeight).Wait();

                //IConsumer consumer = /* initialize consumer */;
                //nano.AddConsumer(consumer);

                // Start consumer, if necessary
                //consumer.Start();

                // Audio & Video client handshaking
                // Sets desired AV formats
                Console.WriteLine("Initializing AV stream (handshaking)...");

                AudioFormat = Nano.AudioFormats[0];
                VideoFormat = Nano.VideoFormats[0];

                Nano.InitializeStreamAsync(AudioFormat, VideoFormat).Wait();

                // Start ChatAudio channel
                // TODO: Send opus audio chat samples to console
                ChatAudioFormat = new AudioFormat(1, 24000, AudioCodec.Opus);
                Nano.OpenChatAudioChannelAsync(ChatAudioFormat).Wait();

                // Tell console to start sending AV frames
                Console.WriteLine("Starting stream...");

                Nano.StartStreamAsync().Wait();

                Shell.WriteLine("Note: Stream is running");
            }
            catch (Exception e)
            {
                Shell.WriteLine($"Error: Failed to init Nano, error: {e}");
                Shell.PressAnyKeyToContinue();
                return;
            }

            // Run a mainloop, to gather controller input events or similar

            FreeConsole();

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Xstream());

            // finally (dirty)
            Process.GetCurrentProcess().Kill();
        }

        async static Task Discover()
        {
            Console.WriteLine("{0,-15} {1,-36} {2,-15} {3,-16}", "Name", "HardwareId", "Address", "LiveId");

            IEnumerable<Device> devices = await Device.DiscoverAsync();
            foreach (Device device in devices)
            {
                Console.WriteLine("{0,-15} {1,-36} {2,-15} {3,-16}",
                    device.Name, device.HardwareId, device.Address, device.LiveId);
            }
        }

        [DllImport("kernel32")]
        public static extern bool AllocConsole();
        [DllImport("kernel32")]
        public static extern bool FreeConsole();
    }
}
