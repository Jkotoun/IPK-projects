using System;
using System.Linq;
using System.Xml.Linq;
using PacketDotNet.Utils;
using SharpPcap;
using SharpPcap.LibPcap;
using System.CommandLine;
using System.IO;
using System.CommandLine.Invocation;

namespace ipk_sniffer
{
    class Program
    {
        //public static void Device_OnPacketArrival(object s, CaptureEventArgs e)
        //{
        //    Console.WriteLine(e.Packet);
        //    //PacketDotNet.Packet.ParsePacket(e.Packet);
        //}

        static public void sniffPackets(string Interface, int port, bool tcp, bool udp, bool icmp, bool arp, int n)
        {
            Console.WriteLine(Interface);
        }

        static int Main(string[] args)
        {

            var rootCommand = new RootCommand
            {
                //TODO predelat description
                new Option<string>( "--interface", "Interface where packets should be sniffed"),
                new Option<int>(new [] {"--port", "-p"}, description: "Specify port of packets to be displayed"),
                new Option<bool>(new [] {"--tcp", "-t"}, description: "filter tcp packets"),
                new Option<bool>(new [] {"--udp", "-u"}, description: "filter udp packets"),
                new Option<bool>("--icmp", description: "filter icmp packets"),
                new Option<bool>("--arp", description: "filter arp frames"),
                new Option<int>("-n", description: "number of packets to display")
            };

            //string selectedInterface = "";
            //int? filteredPort = null;
            //int packetsNumber = 1;
            //bool filter_tcp = false, filter_udp = false, filter_icmp = false, filter_arp = false;

            rootCommand.Handler = CommandHandler.Create<string, int, bool, bool, bool, bool, int>(sniffPackets);
            return rootCommand.InvokeAsync(args).Result;
            
            // Console.WriteLine(selectedInterface);

            //var devices= CaptureDeviceList.Instance;
            //int i = 0;
            //foreach (var print_device in  devices)
            //{
            //    Console.WriteLine(i++ + " " + print_device.Description +  "\n");
            //}
            //Console.WriteLine("Choose device");
            //int index = int.Parse(Console.ReadLine());

            //var device = CaptureDeviceList.Instance[index];

            //device.Open();
            //device.Filter = "icmp";
            //device.OnPacketArrival += Device_OnPacketArrival;
            //device.StartCapture();


            //Console.ReadLine();

            //// Stop the capturing process
            //device.StopCapture();

            //device.Close();


        }
    }
}
