using System;
using System.Linq;
using System.Xml.Linq;
using PacketDotNet.Utils;
using SharpPcap;
using SharpPcap.LibPcap;
using System.CommandLine;
using System.IO;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Net;
using System.Text;
using PacketDotNet;
using PacketDotNet.Lldp;

namespace ipk_sniffer
{
    class Program
    {
        public static string PacketHeader(DateTime arrivalTime, IPAddress sourceAddr, int? sourcePort,
            IPAddress destAddr, int? destPort, int dataLength)
        {
            var header = "";
            header += arrivalTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);;//.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);
            header += $" {sourceAddr}";
            header += sourcePort == null ? "" : $" : {sourcePort}";
            header += $" > {destAddr}";
            header += destPort == null ? "" : $" : {destPort}";
            header += $", length {dataLength} bytes\n\n";
            return header;
        }

        public static string PacketDataHex(string data)
        {
           return string.Join("\n", data.Split("\n").Skip(3).Where(x => x.Length != 0).Select(x => "0x" + x[6..]));
        }
        public static void PrintPacket(Packet packet, DateTime arrivalTime)
        {
            
            
            string data = "";
            if (packet.PayloadPacket is IPv4Packet || packet.PayloadPacket is IPv6Packet)
            {
                var ipPacket = packet.Extract<IPPacket>();
                int? destPort = null, sourcePort = null;
                if (ipPacket.PayloadPacket is UdpPacket || ipPacket.PayloadPacket is TcpPacket)
                {
                    destPort = ipPacket.PayloadPacket is UdpPacket ? ipPacket.Extract<UdpPacket>().DestinationPort : ipPacket.Extract<TcpPacket>().DestinationPort;
                    sourcePort = ipPacket.PayloadPacket is UdpPacket ? ipPacket.Extract<UdpPacket>().SourcePort : ipPacket.Extract<TcpPacket>().SourcePort;
                }


                data = PacketHeader(arrivalTime, ipPacket.SourceAddress, sourcePort, ipPacket.DestinationAddress,
                    destPort, packet.TotalPacketLength);
                data += PacketDataHex(ipPacket.PrintHex());

            }
            else if (packet.PayloadPacket is ArpPacket)
            {
                var arpPacket = packet.Extract<ArpPacket>();
                data = PacketHeader(arrivalTime, arpPacket.SenderProtocolAddress, null, arpPacket.TargetProtocolAddress, null,
                    packet.TotalPacketLength);
                data += PacketDataHex(arpPacket.PrintHex());
            }

            Console.WriteLine($"{data}\n");
        }


        public static void SniffPackets(string Interface, int? p, bool tcp, bool udp, bool icmp, bool arp, int n)
        {
            var devices = CaptureDeviceList.Instance;
            if (Interface == "") //if interface is not given, print list of all interfaces and end execution
            {
                foreach (var device in devices)
                {
                    Console.WriteLine(device.Name);
                }
                return;
            }
            if (devices.All(x => x.Name != Interface))
            {
                Console.Error.WriteLine("Given interface does not exist");
                Environment.Exit(1);
            }

            var deviceToSniff = devices.Single(x => x.Name == Interface);
           
            
            deviceToSniff.Open();
            deviceToSniff.Filter = "";
            if (!(tcp || udp || icmp || arp)) //none of these arguments = sniff all
            {
                if (p == null)
                {
                    deviceToSniff.Filter = "arp or icmp or tcp or udp";
                }
                else
                {
                    deviceToSniff.Filter = $"arp or icmp or ((tcp or udp) and port {p})";
                }
            }
            else
            {
                if (tcp)
                {
                    deviceToSniff.Filter += "tcp";
                }

                if (udp)
                {
                    deviceToSniff.Filter += deviceToSniff.Filter == "" ? "udp" : " or udp";
                }

                if (p != null)
                {
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
                    deviceToSniff.Filter += deviceToSniff.Filter == "" ? "icmp" : " or icmp";
                }
                if (arp)
                {
                    deviceToSniff.Filter += deviceToSniff.Filter == "" ? "arp" : " or arp";
                }

            }
            int i = 0;
            while (i < n)
            {
                var capture = deviceToSniff.GetNextPacket();
                if (capture == null)
                {
                    continue;
                }
                if (capture.LinkLayerType != LinkLayers.Ethernet)
                {
                    continue;
                }
                
                PrintPacket(Packet.ParsePacket(LinkLayers.Ethernet,capture.Data), capture.Timeval.Date);
                i++;
            }
            deviceToSniff.Close();
        }   

        static int Main(string[] args)
        {

            var rootCommand = new RootCommand
            {
                new Option<string?>(new[] {"--interface", "-i"}, "Interface where packets should be sniffed"),
                new Option<int?>("-p", getDefaultValue: () => null, description: "Specify port of packets to be displayed"),
                new Option<bool>(new[] {"--tcp", "-t"}, description: "filter tcp packets"),
                new Option<bool>(new[] {"--udp", "-u"}, description: "filter udp packets"),
                new Option<bool>("--icmp", description: "filter icmp packets"),
                new Option<bool>("--arp", description: "filter arp frames"),
                new Option<int>("-n", getDefaultValue:() => 1, description: "number of packets to display")
            };

            rootCommand.Handler = CommandHandler.Create<string, int?, bool, bool, bool, bool, int>(SniffPackets);
            return rootCommand.InvokeAsync(args).Result;


        }
    }
}
