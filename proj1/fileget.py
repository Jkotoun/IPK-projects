import argparse
import socket
import os
parser = argparse.ArgumentParser()
parser.add_argument('-n', help='Nameserver ip address', dest="nameserver_ip", required=True)
parser.add_argument('-f', help='File to download SURL', dest="download_file_surl", required=True)
args = parser.parse_args()
#TODO predelat sockety na with socket...
#TODO dostat filename z argumentu
#TODO reseni chyb, stazeni vsech souboru - pres index
nameserver_socket= socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
nameserver_socket.connect(("127.0.0.1", 3333))
nameserver_socket.send("WHEREIS file.server.2".encode())
nameserver_recieved = nameserver_socket.recv(1024).decode()
print(nameserver_recieved)
if nameserver_recieved == "ERR Syntax" or nameserver_recieved == "Err Not Found":
    print(f"Nameserver error: {nameserver_recieved}")
else:
    #nameserver response parsing
    _, file_server_address = nameserver_recieved.split(' ')
    file_server_ip, file_server_port = file_server_address.split(':')
    file_server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    #file argument parsing
    file_name = "text.txt" #TODO parsnout z argumentu
    file_server_socket.connect((file_server_ip, int(file_server_port)))
    file_server_socket.send(f"GET {file_name} FSP/1.0\r\nHostname: file.server.2\r\nAgent: xkotou06\r\n\r\n".encode())
    with open(file_name, "wb") as dest_file:
        req_result = file_server_socket.recv(1024).decode().split("\r")[0] #request response - first line of response
        if req_result == "FSP/1.0 Success":
            while True:
                data = file_server_socket.recv(1024)
                if not data:
                    break
                dest_file.write(data)
            dest_file.close()
    file_server_socket.close()
nameserver_socket.close()