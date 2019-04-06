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
using System.IO;
using System.Security.Cryptography;

namespace CS432_Server
{
    public struct Client
    {
        public Client(ref Socket sock, String username, Byte[] password)
        {
            this.socket = sock;
            this.password = password;
            this.username = username;
        }

        public Socket socket;
        public Byte[] password;
        public String username;
    }

    public partial class SecureServerForm : Form
    {
        // Class fields
        const Int32 INVALID = -1;
        Dictionary<String, Client> clients = new Dictionary<string, Client>();

        Socket serverSocket;

        bool serverActive = false;
        Thread connectionHandlerThread;

        byte[] encryptionKey;
        byte[] verificationKey;

        // Constructors
        public SecureServerForm()
        {
            InitializeComponent();
            loadKeys();
        }

        // Utility
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

        private void log(String message)
        {
            if (InvokeRequired)
                Invoke(new Action<string>(textBox_Info.AppendText), message + "\n");
            else
                textBox_Info.AppendText(message + "\n");
        }

        private void destroyClient(String username)
        {
            clients.Remove(username);
            listView_Users.Invoke(
                new MethodInvoker(delegate ()
                {
                    listView_Users.Items.RemoveByKey(username);
                })
            );
        }

        private void listNewUser(String username)
        {
            listView_Users.Invoke(
                new MethodInvoker(delegate ()
                {
                    listView_Users.Items.Add(username);
                })
            );
        }

        private void clearUserList()
        {
            listView_Users.Invoke(
                new MethodInvoker(delegate ()
                {
                    listView_Users.Clear();
                })
            );
        }

        private void parseMessage(String message, String username)
        {
            String[] tokens = message.Split('|');
            switch (tokens[0])
            {
                case "m":
                    log(username + ": " + message);
                    break;
            }
        }

        // Networking
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

            Byte[] passwordHash = buffer.Take(16).ToArray();
            String username = Encoding.Default.GetString(buffer.Skip(16).ToArray());

            // 2 - check if the username already exists, then act accordingly
            if (clients.ContainsKey(username))
            {
                // reject connection
                return "";
            }

            // 3 - if everything is okay, add the new connection to the class-wide clients database & list the client
            clients[username] = new Client(ref sock, username, passwordHash);
            listNewUser(username);

            Thread listener = new Thread(new ParameterizedThreadStart(listenClient));
            listener.Name = username + " listener";
            listener.IsBackground = true;
            listener.Start(clients[username]);

            return username;
        }

        private void handleNewConnection()
        {
            while (serverActive)
            {
                try
                {
                    Socket clientSocket = serverSocket.Accept();

                    String remoteIP = clientSocket.RemoteEndPoint.ToString();
                    log("Connection request from " + remoteIP);

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
                        log("Stopped listening for incoming connections");;
                    }
                }
            }
        }

        private bool socketIsActive(Socket sock)
        {
            return !((sock.Poll(1000, SelectMode.SelectRead) && (sock.Available == 0)) || !sock.Connected);
        }

        private void listenClient(Object client_object)
        {
            Client client = (Client)client_object;
            Socket socket = client.socket;
            while (serverActive)
            {
                bool socketActive = socketIsActive(socket);
                if (!socketActive)
                {
                    log("User " + client.username + " disconnected");
                    destroyClient(client.username);
                }

                if (socket.Available > 0)
                {
                    Byte[] buffer = new Byte[256];
                    socket.Receive(buffer);
                    parseMessage(Encoding.Default.GetString(buffer), client.username);
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
            connectionHandlerThread.IsBackground = true;
            connectionHandlerThread.Start();

            textBox_Info.AppendText("Started listening on port: " + port + "\n");

            button_StartServer.Text = "Stop Server";
        }

        private void stopServer()
        {
            serverActive = false;
            serverSocket.Close();

            clients.Clear();
            clearUserList();

            log("Disconnected from all clients and stopped listening");
            button_StartServer.Text = "Start Server";
        }

        // Cryptography
        static byte[] decryptWithAES128(string input, byte[] key, byte[] IV)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);

            // create AES object from System.Security.Cryptography
            RijndaelManaged aesObject = new RijndaelManaged();
            // since we want to use AES-128
            aesObject.KeySize = 128;
            // block size of AES is 128 bits
            aesObject.BlockSize = 128;
            // mode -> CipherMode.*
            aesObject.Mode = CipherMode.CFB;
            // feedback size should be equal to block size
            // aesObject.FeedbackSize = 128;
            // set the key
            aesObject.Key = key;
            // set the IV
            aesObject.IV = IV;

            // create an encryptor with the settings provided
            ICryptoTransform decryptor = aesObject.CreateDecryptor();
            byte[] result = null;

            result = decryptor.TransformFinalBlock(byteInput, 0, byteInput.Length);

            return result;
        }

        static byte[] hashWithSHA256(string input)
        {
            byte[] byteInput = Encoding.Default.GetBytes(input);

            SHA256CryptoServiceProvider hasher = new SHA256CryptoServiceProvider();

            byte[] result = hasher.ComputeHash(byteInput);

            return result;
        }

        private void loadKeys()
        {
            String encryptedEncryptionKeysStr;
            String encryptedVerificationKeysStr;

            using (System.IO.StreamReader fileReader =
            new System.IO.StreamReader(@"C:\\Users\\oranc\\source\\repos\\CS432-Server\\CS432 Server\\CS432 Server\\keys\\encrypted_server_enc_dec_pub_prv.txt"))
            {
                encryptedEncryptionKeysStr = fileReader.ReadLine();
            }
            using (System.IO.StreamReader fileReader =
            new System.IO.StreamReader(@"C:\\Users\\oranc\\source\\repos\\CS432-Server\\CS432 Server\\CS432 Server\\keys\\encrypted_server_signing_verification_pub_prv.txt"))
            {
                encryptedVerificationKeysStr = fileReader.ReadLine();
            }

            String passphrase = "Bohemian";
            Byte[] hash = hashWithSHA256(passphrase);

            Byte[] aesIV = hash.Take(16).ToArray();
            Byte[] aesKey = hash.Skip(16).ToArray();

            try
            {
                encryptionKey = decryptWithAES128(encryptedEncryptionKeysStr, aesKey, aesIV);
                String sss = Encoding.Default.GetString(encryptionKey);


            }
            catch
            {
                MessageBox.Show(this, "Key Decryption Failed", "Failure", MessageBoxButtons.OK);
            }
        }

        // GUI Events
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
