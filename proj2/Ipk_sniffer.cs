using System;
using System.Linq;
using SharpPcap;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;
#nullable enable
namespace ipk_sniffer
{
    class Ipk_sniffer
    {
        /// <summary>
        /// Create header string of packet from packet header info
        /// </summary>
        /// <param name="arrivalTime">Datetime when packet was captured</param>
        /// <param name="sourceIpAddress">Ip address of source device</param>
        /// <param name="sourcePort">Port of source device, can be null if its icmp or arp packet</param>
        /// <param name="destIpAddress">Ip address of destination</param>
        /// <param name="destPort">Port of destination, can be null if its icmp or arp packet</param>
        /// <param name="dataLength">Length of packet in bytes</param>
        /// <returns>Packet header string</returns>
        public static string PacketHeader(DateTime arrivalTimeUtc, string sourceAddress, int? sourcePort,
            string destAddress, int? destPort, int dataLength)
        {
            var header = "";
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(arrivalTimeUtc, TimeZoneInfo.Local);
            //inspired by https://sebnilsson.com/blog/c-datetime-to-rfc3339-iso-8601/
            header += localTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);//RFC3339 standard format
            header += $" {sourceAddress}";
            header += sourcePort == null ? "" : $" : {sourcePort}"; //icmp and arp have no port
            header += $" > {destAddress}";
            header += destPort == null ? "" : $" : {destPort}";
            header += $", length {dataLength} bytes\n\n";
            return header;
        }
        /// <summary>
        /// Edit format of packet data from printHex method to format required by project assignment
        /// </summary>
        /// <param name="data">Packet data formatted by PrintHex method</param>
        /// <returns>Packet data in format 'offset hexa ascii'</returns>
        public static string PacketDataHex(string data)
        {
            var lines = data.Split("\n");
            //remove first 3 lines - header from print hex, filter only non empty lines, calculate offset in hexa (offset in printHex is wrong)
            //and join offset with rest of string (without data:wrong_offset)
            var linesEdited = lines.Skip(3).Where(x => x.Length != 0).Select((line, index) => "0x" + (index * 16).ToString("x4") + ":" + line[10..]);
            return string.Join("\n", linesEdited);

        }
        /// <summary>
        /// Returns mac address with bytes separated by :, inspired by https://stackoverflow.com/a/22372723, author: Lorenzo Santoro
        /// </summary>
        /// <param name="mac">mac address to format</param>
        /// <returns>formatted mac address separated by :</returns>
        public static string FormatMac(PhysicalAddress mac)
        {
            return string.Join(":", mac.GetAddressBytes().Select(x => x.ToString("X2"))).ToLower();
        }
        /// <summary>
        /// Print tcp, udp, icmp or arp packet in correct format to stdout
        /// </summary>
        /// <param name="packet">Parsed frame to Packet data type</param>
        /// <param name="arrivalTime">Packed captured time</param>
        public static void PrintPacket(RawCapture capture, DateTime arrivalTimeUtc)
        {
            Packet packet = Packet.ParsePacket(capture.LinkLayerType, capture.Data);
            string formattedPacket = "";
            if (packet.PayloadPacket is IPPacket)
            {
                var ipPacket = packet.Extract<IPPacket>();
                int? destPort = null, sourcePort = null;
                //udp or tcp have ports
                if (ipPacket.PayloadPacket is UdpPacket || ipPacket.PayloadPacket is TcpPacket)
                {
                    destPort = ipPacket.PayloadPacket is UdpPacket ? ipPacket.Extract<UdpPacket>().DestinationPort : ipPacket.Extract<TcpPacket>().DestinationPort;
                    sourcePort = ipPacket.PayloadPacket is UdpPacket ? ipPacket.Extract<UdpPacket>().SourcePort : ipPacket.Extract<TcpPacket>().SourcePort;
                }
                formattedPacket = PacketHeader(arrivalTimeUtc, ipPacket.SourceAddress.ToString(), sourcePort, ipPacket.DestinationAddress.ToString(),destPort, capture.Data.Length);
                formattedPacket += PacketDataHex(packet.PrintHex());

            }
            else if (packet.PayloadPacket is ArpPacket)
            {
                var destMac = FormatMac(packet.Extract<EthernetPacket>().DestinationHardwareAddress);
                var senderMac = FormatMac(packet.Extract<EthernetPacket>().SourceHardwareAddress);
                formattedPacket = PacketHeader(arrivalTimeUtc, senderMac, null, destMac, null, capture.Data.Length);//arp has no port - null
                formattedPacket += PacketDataHex(packet.PrintHex());
            }

            Console.WriteLine($"{formattedPacket}\n"); //new line after packet to separate next packet data
        }

        /// <summary>
        /// Capture n packets filtered by arguments and print to stdout
        /// </summary>
        /// <param name="Interface">Interface to capture packets at</param>
        /// <param name="p">Dest or source port of packets to capture</param>
        /// <param name="tcp">Capture tcp packets</param>
        /// <param name="udp">Capture udp packets</param>
        /// <param name="icmp">Capture icmp packets</param>
        /// <param name="arp">Capture arp frames</param>
        /// <param name="n">Number of packets to display</param>
        public static void SniffPackets(string Interface, int? p, bool tcp, bool udp, bool icmp, bool arp, int n)
        {
            var devices = CaptureDeviceList.Instance;
            //if interface is not entered, print list of all interfaces
            if (Interface == "") 
            {
                foreach (var device in devices)
                {
                    Console.WriteLine(device.Name);
                }
                return;
            }
            //none of interfaces match interface option = error
            if (devices.All(x => x.Name != Interface)) 
            {
                Console.Error.WriteLine("Given interface does not exist");
                Environment.Exit(1);
            }
            //select device by interface option
            var deviceToSniff = devices.Single(x => x.Name == Interface);
            deviceToSniff.Open(DeviceMode.Promiscuous);//promiscuous mode expected
            deviceToSniff.Filter = "";
            if (!(tcp || udp || icmp || arp)) //none of these arguments = capture all
            {
                //filter port if -p option was selected
                deviceToSniff.Filter =
                    p == null ? "arp or icmp or icmp6 or tcp or udp" : $"arp or icmp or ((tcp or udp) and port {p})";
            }
            else //set filter by options combination
            {
                if (tcp)
                {
                    deviceToSniff.Filter = "tcp";
                }
                if (udp)
                {
                    deviceToSniff.Filter += deviceToSniff.Filter == "" ? "udp" : " or udp";
                }
                if (p != null)
                {
                    //Tcp or Udp must be selected to capture to filter port
                    if (deviceToSniff.Filter == "") 
                    {
                        Console.Error.WriteLine("icmp and arp have no port");
                        Environment.Exit(1);
                    }
                    else
                    {
                        deviceToSniff.Filter = $"(({deviceToSniff.Filter}) and port {p})";
                    }
                }
                if (icmp)
                {
                    deviceToSniff.Filter += deviceToSniff.Filter == "" ? "icmp or icmp6" : " or icmp or icmp6";
                }
                if (arp)
                {
                    deviceToSniff.Filter += deviceToSniff.Filter == "" ? "arp" : " or arp";
                }

            }
            var i = 0;
            //capture only n packets
            while (i < n) 
            {
                var capture = deviceToSniff.GetNextPacket();
                if (capture == null)
                {
                    continue;
                }
                PrintPacket(capture, capture.Timeval.Date);
                i++;
            }
            deviceToSniff.Close();
        }   

        static void Main(string[] args)
        {
            //definition of supported options
            // inspired by https://github.com/dotnet/command-line-api/blob/main/docs/Your-first-app-with-System-CommandLine.md
            var rootCommand = new RootCommand
            {
                new Option<int?>("-p", getDefaultValue: () => null, description: "Specify port of packets to be displayed"),
                new Option<bool>(new[] {"--tcp", "-t"}, description: "filter tcp packets"),
                new Option<bool>(new[] {"--udp", "-u"}, description: "filter udp packets"),
                new Option<bool>("--icmp", description: "filter icmp packets"),
                new Option<bool>("--arp", description: "filter arp frames"),
                new Option<int>("-n", getDefaultValue:() => 1, description: "number of packets to display")
            };
            //interface option can have 1 or 0 arguments , is required
            var interfaceOption = new Option<string>("--interface", "Interface where packets should be sniffed", arity: ArgumentArity.ZeroOrOne);
            interfaceOption.AddAlias("-i");
            rootCommand.AddOption(interfaceOption);
            //pass parsed arguments to SniffPacket
            rootCommand.Handler = CommandHandler.Create<string, int?, bool, bool, bool, bool, int>(SniffPackets);
            rootCommand.Invoke(args);
        }
    }
}
