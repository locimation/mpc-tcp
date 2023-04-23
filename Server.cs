using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharp.CrestronSockets;
using System.Text.RegularExpressions;

namespace Crestron_TCP_Buttons {

    public delegate void ClientConnected(Client client);
    public delegate void ClientDataReceived(Client client, String data);

    public class Server {

        public TCPServer tcp;
        private Thread thread;
        private Dictionary<uint, Client> clients;

        public CrestronCollection<Button> buttons;

        public event ClientConnected ClientConnectedEvent;
        public event ClientDataReceived ClientDataReceiveEvent;

        public Server() {
            clients = new Dictionary<uint, Client>();
        }

        public void Start() {
            tcp = new TCPServer("0.0.0.0", 9023, 0xFFFF, EthernetAdapterType.EthernetUnknownAdapter, 32);
            tcp.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(SocketStatusChange);
            thread = new Thread(new ThreadCallbackFunction((object o) => { ServerThread(); return null; }), null);
        }

        public void Stop() {
            if(thread != null) thread.Abort();
            if(tcp != null) tcp.DisconnectAll();
            tcp = null;
        }

        public void Broadcast(string msg) {
            lock(clients) {
                var buf = Encoding.ASCII.GetBytes(msg);
                foreach(var client in clients.Values) {
                    client.Send(msg);
                }
            }
        }

        private void ServerThread() {
            uint clientId;
            while(true) {
                var listenStatus = tcp.WaitForConnection(out clientId);
                if(listenStatus == SocketErrorCodes.SOCKET_OK) {
                    lock(clients) {
                        var newClient = new Client(this, tcp, clientId);
                        newClient.DataReceived += new EventHandler<DataEventArgs>(DataReceived);
                        clients.Add(clientId, newClient);
                        ErrorLog.Notice("# ButtonServer: New connection from {0}", tcp.GetAddressServerAcceptedConnectionFromForSpecificClient(clientId));
                        ClientConnectedEvent.Invoke(newClient);
                    }
                } else {
                    ErrorLog.Error("Failed to listen for new connections: {0}", listenStatus.ToString());
                    Thread.Sleep(1000);
                }
            }
        }

        /*void buttonState(Client client) {
            foreach(Button button in buttons) {
                client.Send("BUTTON<" + button.Name + ">=(" + button.State + ")\n");
            }
        }*/

        void DataReceived(object sender, DataEventArgs e) {
            ClientDataReceiveEvent.Invoke(e.client, e.buf);
        }

        // Clean up disconnected clients
        private void SocketStatusChange(TCPServer myTCPServer, uint clientIndex, SocketStatus serverSocketStatus) {
            if(serverSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED) {
                lock(clients) {
                    if(clients.ContainsKey(clientIndex))
                        clients.Remove(clientIndex);
                }
            }
        }


    }

    public class DataEventArgs : EventArgs {
        public string buf;
        public Client client;
    }

    public class Client {

        private uint id;
        private string rx;
        private Server server;

        public event EventHandler<DataEventArgs> DataReceived;

        public Client(Server srv, TCPServer tcp, uint clientId) {
            server = srv;
            id = clientId;
            server.tcp.ReceiveDataAsync(new TCPServerReceiveCallback(ReceiveCallback));
        }

        public void Send(string msg) {
            var buf = Encoding.ASCII.GetBytes(msg);
            if(server.tcp.ClientConnected(id))
                server.tcp.SendData(id, buf, buf.Length);
        }

        private void ReceiveCallback(TCPServer tcp, uint clientId, int len) {

            // Only proceed if we have data & client is connected
            if(len <= 0 || !tcp.ClientConnected(clientId)) return;

            // Append incoming data to string "buffer"
            rx += Encoding.ASCII.GetString(tcp.IncomingDataBuffer, 0, len);

            // Await more data if still connected
            if(tcp.ClientConnected(id))
                tcp.ReceiveDataAsync(clientId, new TCPServerReceiveCallback(ReceiveCallback));

            // Separate messages
            var msgs = rx.Split('\n');

            for(var i = 0; i < msgs.Length - 1; i++)
                try {
                    if(DataReceived != null) {
                        DataReceived.Invoke(this, new DataEventArgs { buf = msgs[i], client = this });
                    }
                } catch { }

            rx = msgs.Last();

        }

    }

}