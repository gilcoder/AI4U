﻿using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace unityremote
{
    public class RemoteBrain : Brain
    {
        public int port = 8081;
        public int buffer_size = 8192; 
        private UdpClient udpSocket;

        private UdpClient socket;
        private bool commandReceived;

        private Socket sock;
        private IPAddress serverAddr;
        private EndPoint endPoint;

        public string remoteIP = "127.0.0.1";
        public int remotePort = 8080;
       
        public Agent agent = null;
        public bool fixedUpdate = true;

        private string cmdname;
        private string[] args;
        private bool message_received = false;
        private bool firstMsgSended = false;
        private System.AsyncCallback async_call;
        private IPEndPoint source;
        // Use this for initialization
        void Start()
        {
            source = null;
            async_call = new System.AsyncCallback(ReceiveData);
            commandReceived = false;
            firstMsgSended = false;
            if (sock == null)
            {
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            serverAddr = IPAddress.Parse(remoteIP);
            endPoint = new IPEndPoint(serverAddr, remotePort);
            Reset();
            agent.SetBrain(this);
            agent.StartData();
        }

        public void Reset()
        {
            try
            {
                udpSocket = new UdpClient(port);
                udpSocket.BeginReceive(async_call, udpSocket);
                //Debug.Log("Listening");
            }
            catch (System.Exception e)
            {
                // Something went wrong
                Debug.Log("Socket error: " + e);
            }
        }

        void OnDisable()
        {
            if (udpSocket != null)
            {
                //Debug.Log("Socket is closed...");
                udpSocket.Close();
            }
        }


        void RemoteUpdate()
        {
            if (message_received)
            {
                this.receivedcmd = cmdname;
                this.receivedargs = args;
                agent.ApplyAction();
                agent.UpdatePhysics();
                message_received = false;
                if (agent.UpdateState())
                {
                    agent.GetState();
                    firstMsgSended = true;
                }
                socket.Client.ReceiveBufferSize = buffer_size;
                socket.BeginReceive(async_call, udpSocket);
            }
            if (!firstMsgSended)
            {
                if (agent.UpdateState())
                {
                    agent.GetState();
                    firstMsgSended = true;
                }
            }
        }

        void FixedUpdate()
        {
            if (fixedUpdate)
            {
                RemoteUpdate();
            }
        }

        void Update()
        {
            if (!fixedUpdate)
            {
                RemoteUpdate();
            }    
        }

        public void ReceiveData(System.IAsyncResult result)
        {
            socket = null;
            try
            {
                
                socket = result.AsyncState as UdpClient;
                if (source == null)
                    source = new IPEndPoint(0, 0);
                
                byte[] data = socket.EndReceive(result, ref source);
                socket.Client.ReceiveBufferSize = 0;
                if (data != null)
                {
                    string cmd = Encoding.UTF8.GetString(data);
                    string[] tokens = cmd.Trim().Split(';');
                    if (tokens.Length < 2)
                    {
                        throw new System.Exception("Invalid command exception: number of arguments is less then two!!!");
                    }
                    string cmdname = tokens[0].Trim();
                    int nargs = int.Parse(tokens[1].Trim());

                    string[] args = new string[nargs];

                    for (int i = 0; i < nargs; i++)
                    {
                        args[i] = tokens[i+2];
                    }
                    this.cmdname = cmdname;
                    this.args = args;
                    message_received = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.Log(e.Message);
                Debug.Log("Inexpected error: " + e.StackTrace);
            } 
        }
   
        public override void SendMessage(string[] desc, byte[] tipo, string[] valor)
        {
            SendMessageFrom(desc, tipo, valor);
        }

        /// <summary>
        /// Envia uma mensagem para o cliente com o seguinte formato:
        ///     [numberofields][[descsize][desc][tipo][valorsize][valor]]+
        ///     onde desc é uma descrição da mensagem, tipo é o tipo da mensagem dado como um inteiro tal que:
        ///         0 = float
        ///         1 = int
        ///         2 = boolean
        ///         3 = string
        ///         4 = array de bytes
        ///    e valor é o valor da informacao enviada.
        /// </summary>
        public void SendMessageFrom(string[] desc, byte[] tipo, string[] valor)
        {
            StringBuilder sb = new StringBuilder();
            int numberoffields = desc.Length;
            sb.Append(numberoffields.ToString().PadLeft(4,' ').PadRight(4, ' '));

            for (int i = 0; i < desc.Length; i++)
            {
                StringBuilder field = new StringBuilder();
                int descsize = Encoding.UTF8.GetByteCount(desc[i]);
                field.Append(descsize.ToString().PadLeft(4, ' ').PadRight(4,' '));
                field.Append(desc[i]);
                field.Append((int)tipo[i]);
                field.Append(Encoding.UTF8.GetByteCount(valor[i]).ToString().PadLeft(8, ' ').PadRight(8, ' '));
                field.Append(valor[i]);
                string fstr = field.ToString();
                sb.Append(fstr);
            }
            byte[] b = Encoding.UTF8.GetBytes(sb.ToString());
            this.sendData(b);
        }

        public void sendData(byte[] data)
        {
            sock.SendTo(data, endPoint);
        }
    }
}
