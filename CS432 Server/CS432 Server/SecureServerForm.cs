using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CS432_Server
{
    public struct Client
    {
        public Client(ref Socket sock, Byte[] password)
        {
            this.socket = sock;
            this.password = password;
        }

        public Socket socket;
        public Byte[] password;
    }

    public partial class SecureServerForm : Form
    {
        const Int32 INVALID = -1;
        Dictionary<String, Client> clients = new Dictionary<string, Client>();

        Socket serverSocket;

        bool serverActive = false;
        Thread connectionHandlerThread;

        public SecureServerForm()
        {
            InitializeComponent();
        }

        private Int32 validatePort()
        {
            string str_port = textBox_Port.Text;
            Int32 int_port;

            try
            {
                int_port = Convert.ToInt32(str_port);

                if (0 > int_port || int_port > 65535)
                {
                    int_port = INVALID;
                }
            }
            catch
            {
                int_port = INVALID;
            }

            return int_port;
        }

        private String processIncommingConnection(ref Socket sock)
        {
            // 1 - initialize a buffer for the incoming message and listen for the message
            sock.ReceiveTimeout = 2000;
            Byte[] buffer = new byte[256];
            int received_bytes = sock.Receive(buffer);

            if (received_bytes == 0)
            {
                return "";
            }

            // DEBUG: ARE THE ARGUMENTS GIVEN BELOW BYTES OR BITS
            Byte[] passwordHash = buffer.Take(16).ToArray();
            String username = Encoding.Default.GetString(buffer.Skip(16).ToArray());

            // 2 - check if the username already exists, then act accordingly
            if (clients.ContainsKey(username))
            {
                // reject connection
                return "";
            }

            // 3 - if everything is okay, add the new connection to the class-wide clients database & list the client
            clients[username] = new Client(ref sock, passwordHash);
            listView_Users.Items.Add(username);

            return username;
        }

        private void log(String message)
        {
            textBox_Info.AppendText(message + "\n");
        }

        private void handleNewConnection()
        {
            while (serverActive)
            {
                try
                {
                    Socket clientSocket = serverSocket.Accept();


                    String remoteIP = clientSocket.RemoteEndPoint.ToString();
                    Invoke(new Action<string>(log), "Incoming connection from " + remoteIP);

                    string connectedUser = processIncommingConnection(ref clientSocket);

                    if (connectedUser == "")
                    {
                        log("Connection from " + remoteIP + " is rejected");
                        continue;
                    }

                    log("Connected to user: " + connectedUser);
                }
                catch
                {
                    if (!serverActive)
                    {
                        Invoke(new Action<string>(log), "Stopped listening for incoming connections");
                    }
                }
            }
        }

        private void startServer()
        {
            // 1 - initialize the server socket
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 2 - receive the port number and validate it
            Int32 port = validatePort();
            if (port == INVALID)
            {
                MessageBox.Show(this, "Please enter a valid port", "Invalid Port", MessageBoxButtons.OK);
                textBox_Port.Text = "";
                return;
            }

            // 3 - bind the server socket to the port and listen for incoming connections
            serverActive = true;
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(3);

            connectionHandlerThread = new Thread(new ThreadStart(handleNewConnection));
            connectionHandlerThread.Start();

            textBox_Info.AppendText("Started listening on port: " + port + "\n");

            button_StartServer.Text = "Stop Server";
        }

        private void stopServer()
        {
            serverActive = false;
            serverSocket.Close();
            log("Disconnected from all clients and stopped listening");
            button_StartServer.Text = "Start Server";
        }

        private void button_StartServer_Click(object sender, EventArgs e)
        {
            if (!serverActive)
            {
                startServer();
            }
            else
            {
                stopServer();
            }
        }
    }
}
