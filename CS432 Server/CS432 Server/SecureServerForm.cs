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
    public class Client
    {
        public Client(ref Socket sock, String username, Byte[] password)
        {
            this.socket = sock;
            this.password = password;
            this.username = username;
            this.alive = true;
            this.listening = true;
        }

        public Socket socket;
        public Byte[] password;
        public String username;
        public bool alive; // has the server decided to terminate the connection?
        public bool listening; // did client send a connection termination request?
        public Byte[] authChallengeValue = null;
        public bool isAuthenticated = false;
    }

    public partial class SecureServerForm : Form
    {
        // Class fields
        const Int32 INVALID = -1;
        private RNGCryptoServiceProvider Rand = new RNGCryptoServiceProvider();
        String databaseFile = "database.txt";

        Dictionary<String, Client> clients = new Dictionary<string, Client>();
        Dictionary<String, Byte[]> enrolledMembers = new Dictionary<String, Byte[]>();

        Socket serverSocket;

        bool serverActive = false;
        Thread connectionHandlerThread;

        byte[] encryptionKey;
        byte[] verificationKey;

        Int16 userTempId = 0;

        // Constructors
        public SecureServerForm()
        {
            InitializeComponent();
            loadKeys();
            loadEnrolledMembers();
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

        private void destroyActiveClient(String clientId)
        {
            Client client = clients[clientId];
            client.listening = false;
            
            // wait for listener thread for this client to terminate
            while (client.alive)
            {
                Thread.Sleep(25);
            }
            clients.Remove(clientId);

            listView_Users.Invoke(
                new MethodInvoker(delegate ()
                {
                    listView_Users.Items.RemoveByKey(clientId);
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

        private Byte[] random128bit()
        {
            byte[] randBytes = new byte[16];
            Rand.GetBytes(randBytes);

            return randBytes;
        }

        private static string generateHexStringFromByteArray(byte[] input)
        {
            string hexString = BitConverter.ToString(input);
            return hexString.Replace("-", "");
        }

        private static byte[] hexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private String getClientId()
        {
            String tempId = "__client_" + userTempId.ToString();
            userTempId++;
            return tempId;
        }

        // Networking
        private void parseMessage(String message, String clientId)
        {
            char flag = message[0];
            String content = message.Substring(2);

            Socket sock = clients[clientId].socket;

            switch (flag)
            {
                case 'm': // broadcast message sent by the client
                    log(sock.RemoteEndPoint.ToString() + ": " + content);
                    break;
                case 'a': // authroization request
                    initiateAuthentication(clientId, content);
                    break;
                case 'e': // enrollment request
                    enrollUser(clientId, Encoding.Default.GetBytes(content));
                    break;
                case 'h':
                    verifyAuthentication(clientId, Encoding.Default.GetBytes(content));
                    break;
                case 'd':
                    destroyActiveClient(clientId);
                    break;
            }
        }

        private void initiateAuthentication(String clientId, String username)
        {
            // 1 - set the username of the socket with the provided value
            Client currentClient = clients[clientId];
            currentClient.username = username;

            // 2 - generate random 128 bit numbers and send it
            Byte[] randomBytes = random128bit();
            transmitClear(randomBytes, ref currentClient.socket, "a");

            // 3 - save the sent random bytes
            currentClient.authChallengeValue = randomBytes;
        }

        private void verifyAuthentication(String clientId, Byte[] receivedHMAC)
        {
            Client currentClient = clients[clientId];
            Byte[] userPassword = enrolledMembers[currentClient.username];
            Byte[] hmacValue = applyHMACwithSHA256(Encoding.Default.GetString(currentClient.authChallengeValue), userPassword);

            if (receivedHMAC == hmacValue)
            {
                transmitSigned("ack_positive", ref currentClient.socket, "a");
                log("Authorization to " + currentClient.username + " successful!");
                currentClient.isAuthenticated = true;
            }
            else
            {
                transmitSigned("ack_negative", ref currentClient.socket, "a");
                log("Authorization to " + currentClient.username + " failed!");
                destroyActiveClient(clientId);
            }
        }

        private void transmitSigned(String message, ref Socket sock, String flag)
        {
            Byte[] signature = signWithRSA3072(message); // 384 bytes
            Byte[] encodedMessage = Encoding.Default.GetBytes(message);
            Byte[] encodedFlag = Encoding.Default.GetBytes(flag + "|");

            Byte[] finalMessage = encodedFlag.Concat(signature.Concat(encodedMessage)).ToArray();

            String finalMessageString = Encoding.Default.GetString(finalMessage);

            sock.Send(finalMessage);
        }

        private void transmitClear(String message, ref Socket sock, String flag)
        {
            Byte[] encodedMessage = Encoding.Default.GetBytes(flag + "|" + message);
            sock.Send(encodedMessage);
        }

        private void transmitClear(Byte[] message, ref Socket sock, String flag)
        {
            Byte[] transmissionMessage = Encoding.Default.GetBytes(flag + "|").Concat(message).ToArray();
            int sentBytes = sock.Send(transmissionMessage);
        }

        private void enrollUser(String clientId, Byte[] received)
        {
            // Enrolls a user to the database and returns <username, passwordHash

            Socket sock = clients[clientId].socket;

            // 1 - initialize a buffer for the incoming message and listen for the 
            Byte[] decryptedBuffer = decryptWithRSA3072(received);

            Byte[] passwordHash = decryptedBuffer.Take(16).ToArray();
            String username = Encoding.Default.GetString(decryptedBuffer.Skip(16).ToArray());

            // 2 - check if the username already exists, then act accordingly
            if (enrolledMembers.ContainsKey(username))
            {
                // reject connection
                transmitSigned("error", ref sock, "e");
                return;
            }

            // 3 - if everything is okay, add the new connection  database & list the 
            saveUser(username, passwordHash);
            enrolledMembers[username] = passwordHash;
            clients[clientId].username = username;
            clients[clientId].password = passwordHash;

            log("Successfully enrolled user " + username);
            transmitSigned("success", ref sock, "e");
        }

        private void listenClient(Object clientId)
        {
            String id = (String)clientId;
            Client currentClient = clients[id];

            Socket socket = currentClient.socket;
            bool keepListening = currentClient.listening;

            while (serverActive && keepListening)
            {
                if (socket.Available > 0)
                {
                    Byte[] buffer = new Byte[8192];
                    int receivedBytes = socket.Receive(buffer);
                    buffer = buffer.Take(receivedBytes).ToArray();

                    parseMessage(Encoding.Default.GetString(buffer), id);
                }

                keepListening = currentClient.listening;
            }
        
            transmitClear("", ref socket, "d"); // OLASI PROBLEM
            socket.Close();

            currentClient.alive = false;
        }

        private void acceptNewConnection()
        {
            while (serverActive)
            {
                try
                {
                    Socket clientSocket = serverSocket.Accept();

                    String remoteIP = clientSocket.RemoteEndPoint.ToString();
                    log("New connection from " + remoteIP);

                    String clientId = getClientId();
                    clients[clientId] = new Client(ref clientSocket, "", null);

                    Thread socketListener = new Thread(new ParameterizedThreadStart(listenClient));
                    socketListener.IsBackground = true;
                    socketListener.Start(clientId);
                }
                catch (CryptographicException e)
                {
                    log("Cryptographic Exception occured: " + e.Message);
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

            connectionHandlerThread = new Thread(new ThreadStart(acceptNewConnection));
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
        byte[] decryptWithRSA3072(byte[] byteInput)
        {
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(3072);
            // set RSA object with xml string
            String RSAXmlString = Encoding.Default.GetString(encryptionKey);
            rsaObject.FromXmlString(RSAXmlString);
            return rsaObject.Decrypt(byteInput, true);
        }

        byte[] encryptWithRSA3072(string input)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(3072);
            // set RSA object with xml string
            rsaObject.FromXmlString(Encoding.Default.GetString(encryptionKey));
            return rsaObject.Encrypt(byteInput, true);
        }

        byte[] signWithRSA3072(string input)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create RSA object from System.Security.Cryptography
            RSACryptoServiceProvider rsaObject = new RSACryptoServiceProvider(3072);
            // set RSA object with xml string
            rsaObject.FromXmlString(Encoding.Default.GetString(verificationKey));
            byte[] result = null;

            try
            {
                result = rsaObject.SignData(byteInput, "SHA256");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        static byte[] decryptWithAES128(byte[] byteInput, byte[] key, byte[] IV)
        {
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

        static byte[] applyHMACwithSHA256(string input, Byte[] key)
        {
            // convert input string to byte array
            byte[] byteInput = Encoding.Default.GetBytes(input);
            // create HMAC applier object from System.Security.Cryptography
            HMACSHA256 hmacSHA256 = new HMACSHA256(key);
            // get the result of HMAC operation
            byte[] result = hmacSHA256.ComputeHash(byteInput);

            return result;
        }

        private void loadKeys()
        {
            String encryptedEncryptionKeysStr;
            String encryptedVerificationKeysStr;

            using (System.IO.StreamReader fileReader =
            new System.IO.StreamReader(@"C:\\Users\\oranc\\Desktop\\Course stuff\\cs432 prroject 1\\encrypted_server_enc_dec_pub_prv.txt"))
            {
                encryptedEncryptionKeysStr = fileReader.ReadLine();
            }
            using (System.IO.StreamReader fileReader =
            new System.IO.StreamReader(@"C:\\Users\\oranc\\Desktop\\Course stuff\\cs432 prroject 1\\encrypted_server_signing_verification_pub_prv.txt"))
            {
                encryptedVerificationKeysStr = fileReader.ReadLine();
            }
        
            String passphrase = "Bohemian";
            Byte[] hash = hashWithSHA256(passphrase);

            Byte[] aesIV = hash.Take(16).ToArray();
            Byte[] aesKey = hash.Skip(16).ToArray();

            try
            {
                encryptionKey = decryptWithAES128(hexStringToByteArray(encryptedEncryptionKeysStr), aesKey, aesIV);
                verificationKey = decryptWithAES128(hexStringToByteArray(encryptedVerificationKeysStr), aesKey, aesIV);
                log("Encryption and verification keys have been loaded from the file system");
            }
            catch
            {
                MessageBox.Show(this, "Key Decryption Failed", "Failure", MessageBoxButtons.OK);
            }
        }

        // User Database Operations
        void saveUser(String username, Byte[] passwordHash)
        {
            using (System.IO.StreamWriter file =
            new System.IO.StreamWriter(databaseFile))
            {
                file.WriteLine(username + " " + generateHexStringFromByteArray(passwordHash));
            }
        }

        void loadEnrolledMembers()
        {
            try
            {
                if (!File.Exists(databaseFile))
                {
                    log("Database file not existing, creating one.");
                    File.Create(databaseFile);
                    return;
                }

                enrolledMembers = new Dictionary<String, Byte[]>();

                string line;
                using (System.IO.StreamReader fileReader =
                new System.IO.StreamReader(databaseFile))
                {
                    line = fileReader.ReadLine();

                    if (line == null)
                    {
                        return;
                    }

                    String[] parsedLine = line.Split(' ');
                    enrolledMembers[parsedLine[0]] = hexStringToByteArray(parsedLine[1]);
                }

            }
            catch
            {
                MessageBox.Show(this, "An error occured during loading enrolled users", "Error", MessageBoxButtons.OK);
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
