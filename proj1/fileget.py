import argparse
import socket
import os
import re
import sys
def downloadFile(file_name, fileserver_name, tcp_file_server_socket, keep_dir_structure):
    try:
        tcp_file_server_socket.send(f"GET {file_name} FSP/1.0\r\nHostname: {fileserver_name}\r\nAgent: xkotou06\r\n\r\n".encode())
        first_response = tcp_file_server_socket.recv(1024)
        file_server_req_result = first_response.split(b"\r\n")[0].decode()  # request status from first response
        first_response_data = b"\r\n".join(first_response.split(b"\r\n")[3:])  # remove header from first data response
        if file_server_req_result == "FSP/1.0 Success":
            data = first_response_data
            if keep_dir_structure == True:
                dest_path = file_name
            else: 
                dest_path =os.path.basename(file_name)
            with open(dest_path , "wb") as dest_file:
                while True:
                    dest_file.write(data)
                    data = tcp_file_server_socket.recv(1024)
                    if not data:
                        break
        else:
            raise ConnectionError(file_server_req_result + "\n" + first_response_data.decode())
    except:
        raise
parser = argparse.ArgumentParser()
parser.add_argument("-n", help="Nameserver ip address", dest="nameserver_ip", required=True)
parser.add_argument("-f", help="File to download SURL", dest="download_file_surl", required=True)
args = parser.parse_args()
# first regex - ipaddr:port format
# second regex - fsp://SERVER_NAME/PATH format, servername can be alfanum, _, - and .
if not (re.match(r"^([0-9]{1,3}\.){3}[0-9]{1,3}:[0-9]+$", args.nameserver_ip) and re.match(r"^(FSP|fsp)://[a-zA-Z0-9_.\-]+/.+$", args.download_file_surl)):
    print("Arguments format error", file=sys.stderr)
    sys.exit(1)
with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as nameserver_udp_socket:
    nameserver_ip, nameserver_port = args.nameserver_ip.split(":")
    fileserver_name = args.download_file_surl.split("/")[2]
    try:
        nameserver_udp_socket.sendto(f"WHEREIS {fileserver_name}".encode(), (nameserver_ip, int(nameserver_port)))
        nameserver_udp_socket.settimeout(5)
        nameserver_recieved = nameserver_udp_socket.recv(1024).decode()
    except socket.timeout:
        print("Nameserver not responding...", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(e, file=sys.stderr)
        sys.exit(1)
if nameserver_recieved == "ERR Syntax" or nameserver_recieved == "Err Not Found":
    print(f"Nameserver error: {nameserver_recieved}", file=sys.stderr)  # TODO error code
    sys.exit(1)
elif re.match(r"OK.*", nameserver_recieved):
    _, file_server_address = nameserver_recieved.split(" ")
    file_server_ip, file_server_port = file_server_address.split(":")
    file_name = "/".join(args.download_file_surl.split("/")[3:])
    if file_name == "*":
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as tcp_file_server_socket:
                tcp_file_server_socket.settimeout(5)
                tcp_file_server_socket.connect((file_server_ip, int(file_server_port)))
                tcp_file_server_socket.send(f"GET index FSP/1.0\r\nHostname: {fileserver_name}/test\r\nAgent: xkotou06\r\n\r\n".encode())
                first_response= tcp_file_server_socket.recv(1024)
                index_req_result = first_response.split(b"\r\n")[0].decode()  # request status from first response
                if index_req_result == "FSP/1.0 Success":
                    files_to_download = first_response.decode().split("\r\n")[3:]
                    while True:
                        response = tcp_file_server_socket.recv(5000)
                        if not response:
                            break
                        files_to_download.extend(response.decode().split("\r\n"))  
            for file in files_to_download:
                if file != "":
                    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as newsock:
                        newsock.connect((file_server_ip, int(file_server_port)))
                        file_dir =os.path.dirname(file) 
                        if file_dir != "" and not os.path.exists(file_dir): 
                            os.makedirs(file_dir)
                        downloadFile(file,fileserver_name,newsock, True)
        except socket.timeout:
            print("Fileserver not responding...", file=sys.stderr)
            sys.exit(1)
        except Exception as e:
            print(e, file=sys.stderr)
            sys.exit(1)  
    else:
        try:
             with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as newsock:
                newsock.connect((file_server_ip, int(file_server_port)))
                downloadFile(file_name, fileserver_name, newsock, False)
        except socket.timeout:
            print("Fileserver not responding...", file=sys.stderr)
            sys.exit(1)
        except Exception as e:
            print(e, file=sys.stderr)
            sys.exit(1)
