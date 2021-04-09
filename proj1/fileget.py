#!/usr/bin/env python3
import argparse
import socket
import os
import re
import sys
#download file 'file_name' from server 'fileserver_name' connected by tcp socket 'tcp_file_server_socket'
#return file data in bytes
def downloadFileData(file_path, fileserver_name, tcp_file_server_socket):
    try:
        tcp_file_server_socket.send(f"GET {file_path} FSP/1.0\r\nHostname: {fileserver_name}\r\nAgent: xkotou06\r\n\r\n".encode())
        first_response_splitted_lines = tcp_file_server_socket.recv(1024).split(b"\r\n")
        file_server_req_result = first_response_splitted_lines[0].decode()  # request status
        if file_server_req_result == "FSP/1.0 Success":
            first_recieved_data = b"\r\n".join(first_response_splitted_lines[3:])  # get only data from first response
            bytes_remaining = int(first_response_splitted_lines[1].decode().split(":")[1]) - len(first_recieved_data)
            file_data = first_recieved_data
            while True:
                recieved_data = tcp_file_server_socket.recv(1024)
                if not recieved_data:
                    break
                file_data += recieved_data
                bytes_remaining -= len(recieved_data)
            if bytes_remaining != 0:
                raise ConnectionAbortedError("Connection aborted")
            return file_data
        else:
            raise ConnectionError(file_server_req_result + "\n" + first_recieved_data.decode())
    except:
        raise
#save bytes to 'file_name! file
#keep_dir_structure - true - file is downloaded to same directory as in file server, False - downloaded to script directory
def saveBytesToFile(bytes, file_name, keep_dir_structure):
    if keep_dir_structure == True:
        file_dir =os.path.dirname(file_name) #path to directory of file 
        if file_dir != "": #if file is in subdir, create subdir structure to keep directory hierarchy from file server
            os.makedirs(file_dir, exist_ok = True)
        dest_path = file_name #create file in same subdir as in file server
    else: 
        dest_path =os.path.basename(file_name) #create file in root (script directory)
    with open(dest_path , "wb") as dest_file:
        dest_file.write(bytes)
parser = argparse.ArgumentParser()
parser.add_argument("-n", help="Nameserver ip address", dest="nameserver_ip", required=True)
parser.add_argument("-f", help="File to download SURL", dest="download_file_surl", required=True)
args = parser.parse_args()
# first regex - ipaddr:port format
# second regex - fsp://SERVER_NAME/PATH format, servername can be alfanum, _, - and .
if not (re.match(r"^([0-9]{1,3}\.){3}[0-9]{1,3}:[0-9]+$", args.nameserver_ip) and re.match(r"^(FSP|fsp)://[a-zA-Z0-9_.\-]+/.+$", args.download_file_surl)):
    print("Arguments format error", file=sys.stderr)
    sys.exit(1)
with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as nameserver_udp_socket: #create UDP socket
    nameserver_ip, nameserver_port = args.nameserver_ip.split(":")
    fileserver_name = args.download_file_surl.split("/")[2] #format fsp://server - index 2 is server name
    try:
        nameserver_udp_socket.sendto(f"WHEREIS {fileserver_name}".encode(), (nameserver_ip, int(nameserver_port))) #send request for file server ip
        nameserver_udp_socket.settimeout(5)
        nameserver_recieved = nameserver_udp_socket.recv(1024).decode()
    except socket.timeout:
        print("Nameserver not responding...", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(e, file=sys.stderr)
        sys.exit(1)
if re.match(r"OK.*", nameserver_recieved):
    _, file_server_address = nameserver_recieved.split(" ") #format OK ip- need only ip
    file_server_ip, file_server_port = file_server_address.split(":")
    file_name = "/".join(args.download_file_surl.split("/")[3:]) #extract file path from fsp://server/file/path format
    if file_name == "*": #download all files from server
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as tcp_file_server_socket: #create tcp socket
                tcp_file_server_socket.settimeout(5)
                tcp_file_server_socket.connect((file_server_ip, int(file_server_port)))
                server_index_data = downloadFileData("index", fileserver_name, tcp_file_server_socket)
                files_to_download = server_index_data.decode().split("\r\n")  #create list of files to download from index
            for file in files_to_download:
                if file != "":
                    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as newsock:
                        newsock.connect((file_server_ip, int(file_server_port)))
                        newsock.settimeout(5)
                        fileData = downloadFileData(file,fileserver_name,newsock)
                        saveBytesToFile(fileData, file,True)
        except socket.timeout:
            print("Fileserver not responding...", file=sys.stderr)
            sys.exit(1)
        except ConnectionAbortedError as e:
            print("Connection aborted")
            sys.exit(1)
        except Exception as e:
            print("Connection error\n")
            print(e, file=sys.stderr)
            sys.exit(1)  
    else: #download given file to root dir
        try:
             with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as newsock:
                newsock.settimeout(5)
                newsock.connect((file_server_ip, int(file_server_port)))
                fileData = downloadFileData(file_name, fileserver_name, newsock)
                saveBytesToFile(fileData, file_name,False)
        except socket.timeout:
            print("Fileserver not responding...", file=sys.stderr)
            sys.exit(1)
        except ConnectionAbortedError as e:
            print("Connection aborted")
            sys.exit(1)
        except Exception as e:
            print(e, file=sys.stderr)
            sys.exit(1)
else:
    print(f"Nameserver error: {nameserver_recieved}", file=sys.stderr)
    sys.exit(1)
