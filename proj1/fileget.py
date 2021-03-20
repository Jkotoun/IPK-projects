import argparse
import socket
import os
import re
import sys

def fileServerRe(arg_value, pattern=re.compile(r"[0-9]*")):
    if not pattern.match(arg_value):
        raise argparse.ArgumentTypeError
    return arg_value




parser = argparse.ArgumentParser()
parser.add_argument('-n', help='Nameserver ip address', dest="nameserver_ip", required=True)
parser.add_argument('-f', help='File to download SURL', dest="download_file_surl", required=True)
args = parser.parse_args()
# first regex - ipaddr:port format
# second regex - fsp://SERVER_NAME/PATH format, servername can be alfanum, _, - and . 
if not (re.match(r"^([0-9]{1,3}\.){3}[0-9]{1,3}:[0-9]+$", args.nameserver_ip) and re.match(r"^(FSP|fsp)://[a-zA-Z0-9_.\-]+/.+$", args.download_file_surl)):
    print("Arguments format error", file=sys.stderr)
    sys.exit(1)
#TODO reseni chyb, stazeni vsech souboru - pres index

with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as nameserver_udp_socket:
    nameserver_ip, nameserver_port = args.nameserver_ip.split(":")
    fileserver_name = args.download_file_surl.split('/')[2]
    file_name = '/'.join(args.download_file_surl.split('/')[3:])
    try:
        nameserver_udp_socket.sendto(f"WHEREIS {fileserver_name}".encode(), (nameserver_ip, int(nameserver_port)))
        nameserver_udp_socket.settimeout(2)
        nameserver_recieved = nameserver_udp_socket.recv(1024).decode()
    except socket.timeout:
        print("Nameserver not responding...", file=sys.stderr)
        sys.exit(1)
    except Exception:
        print("Unexpected error", file=sys.stderr)
        sys.exit(1)
    if nameserver_recieved == "ERR Syntax" or nameserver_recieved == "Err Not Found":
        print(f"Nameserver error: {nameserver_recieved}") #TODO error code
        sys.exit(1)
    elif re.match(r"OK.*", nameserver_recieved):
        _, file_server_address = nameserver_recieved.split(' ')
        file_server_ip, file_server_port = file_server_address.split(':')
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as tcp_file_server_socket:
            tcp_file_server_socket.settimeout(2)
            try:
                tcp_file_server_socket.connect((file_server_ip, int(file_server_port)))
                tcp_file_server_socket.send(f"GET {file_name} FSP/1.0\r\nHostname: {fileserver_name}\r\nAgent: xkotou06\r\n\r\n".encode())
                first_response = tcp_file_server_socket.recv(1024)
                file_server_req_result = first_response.split(b'\r\n')[0].decode() #request status from first response
                first_response_data = (b'\r\n'.join(first_response.split(b'\r\n')[3:])) #remove header from first data response
                if file_server_req_result == "FSP/1.0 Success":
                    data = first_response_data
                    with open(file_name, "wb") as dest_file:
                        while True:
                            dest_file.write(data)
                            data = tcp_file_server_socket.recv(1024)
                            if not data:
                                break
                else:
                    print(first_response_data, file=sys.stderr)
                    sys.exit(1)  
            except socket.timeout:
                print("Fileserver not responding...", file=sys.stderr)
                sys.exit(1)
            except Exception:
                print("Connection error")
                sys.exit(1)

