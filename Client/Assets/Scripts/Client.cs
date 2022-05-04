using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    public string ip = "188.252.190.3";
    public int port = 26950;
    public int myId = 0;
    public TCP tcp;
    public UDP udp;

    private bool isConnected = false;
    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists.");
            Destroy(this);

        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        tcp = new TCP();
        udp = new UDP();

        InitializeClientData();
        isConnected = true;
        tcp.Connect();
    }

    public class UDP
    {
        public UdpClient socket;
        public IPEndPoint endPoint;

        public UDP()
        {
            endPoint = new IPEndPoint(IPAddress.Parse(instance.ip), instance.port);
        }

        public void Connect(int _localPort)
        {
            socket = new UdpClient(_localPort);
            socket.Connect(endPoint);
            socket.BeginReceive(RecieveCallback, null);

            using(Packet _packet=new Packet())
            {
                SendData(_packet);
            }
        }

        public void SendData(Packet _packet)
        {
            try
            {
                _packet.InsertInt(instance.myId);
                if (socket != null)
                {
                    socket.BeginSend(_packet.ToArray(), _packet.Length(), null, null);
                }
            }
            catch(Exception ex)
            {
                Debug.Log($"Error ending data to server via UDP: {ex}");
            }
        }

        private void RecieveCallback(IAsyncResult _result)
        {
            try
            {
                byte[] _data = socket.EndReceive(_result, ref endPoint);
                socket.BeginReceive(RecieveCallback, null);

                if (_data.Length < 4)
                {
                    instance.Disconnect();
                    return;
                }

                HandleData(_data);
            }
            catch(Exception ex)
            {
                Disconnect();
            }
        }

        private void HandleData(byte[] _data)
        {
            using(Packet _packet=new Packet(_data))
            {
                int _packetLength = _packet.ReadInt();
                _data = _packet.ReadBytes(_packetLength);
            }

            ThreadManager.ExecuteOnMainThread(() =>
            {
                using (Packet _packet = new Packet(_data))
                {
                    int _packetID = _packet.ReadInt();
                    packetHandlers[_packetID](_packet);
                }
            });
        }

        private void Disconnect()
        {
            instance.Disconnect();

            endPoint = null;
            socket = null;
        }
    }

    public class TCP
    {
        public TcpClient socket;
        private NetworkStream stream;
        private byte[] receivedBuffer;

        private Packet receivedData;

        public void Connect()
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            receivedBuffer = new byte[dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
            Debug.Log(socket.Client.RemoteEndPoint);

        }

        private void ConnectCallback(IAsyncResult _result)
        {
            socket.EndConnect(_result);

            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            receivedData = new Packet();

            stream.BeginRead(receivedBuffer, 0, dataBufferSize, RecieveCallBack, null);

        }

        public void SendData(Packet _packet)
        {
            try
            {
                if (socket != null)
                {
                    stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                }
            }
            catch(Exception ex)
            {
                Debug.Log($"Error sending data to server via TCP: {ex}");
            }
        }


        private void RecieveCallBack(IAsyncResult _result)
        {
            try
            {
                int _byteLength = stream.EndRead(_result);
                if (_byteLength <= 0)
                {
                    instance.Disconnect();
                    return;
                }

                byte[] _data = new byte[_byteLength];
                Array.Copy(receivedBuffer, _data, _byteLength);

                receivedData.Reset(HandleData(_data));

                stream.BeginRead(receivedBuffer, 0, dataBufferSize, RecieveCallBack, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recieving TCP data: {ex}");
                Disconnect();
            }
        }

        private bool HandleData(byte[] _data)
        {
            int _packetLenght = 0;

            receivedData.SetBytes(_data);

            if (receivedData.UnreadLength() >= 4)
            {
                _packetLenght = receivedData.ReadInt();
                if (_packetLenght <= 0)
                {
                    return true;
                }
            }

            while (_packetLenght > 0 && _packetLenght <= receivedData.UnreadLength())
            {
                byte[] _packetBytes = receivedData.ReadBytes(_packetLenght);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        packetHandlers[_packetId](_packet);
                    }
                });

                _packetLenght = 0;

                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLenght = receivedData.ReadInt();
                    if (_packetLenght <= 0)
                    {
                        return true;
                    }
                }
            }

            if (_packetLenght <= 1)
            {
                return true;
            }
            return false;
        }

        private void Disconnect()
        {
            instance.Disconnect();

            stream = null;
            receivedData = null;
            receivedBuffer = null;
            socket = null;
        }
    }

    private void InitializeClientData()
    {
        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            {(int)ServerPackets.welcome,ClientHandle.Welcome },
            {(int)ServerPackets.udpTest,ClientHandle.UDPTest },
            {(int)ServerPackets.spawnPlayer,ClientHandle.SpawnPlayer },
            {(int)ServerPackets.playerPosition,ClientHandle.PlayerPosition },
            {(int)ServerPackets.playerRotation,ClientHandle.PlayerRotation },
            {(int)ServerPackets.playerDisconnected,ClientHandle.PlayerDisconnected },
            {(int)ServerPackets.playerHealth,ClientHandle.PlayerHealth },
            {(int)ServerPackets.playerRespawned,ClientHandle.PlayerRespawned },
            {(int)ServerPackets.createItemSpawner,ClientHandle.CreateItemSpawner },
            {(int)ServerPackets.itemSpawned,ClientHandle.ItemSpawned },
            {(int)ServerPackets.itemPickedUp,ClientHandle.ItemPickedUp },
            {(int)ServerPackets.spawnProjectile,ClientHandle.SpawnProjectile },
            {(int)ServerPackets.projectilePosition,ClientHandle.ProjectilePosition },
            {(int)ServerPackets.projectileExplode,ClientHandle.ProjectileExplode },
            {(int)ServerPackets.spawnEnemy,ClientHandle.SpawnEnemy },
            {(int)ServerPackets.enemyPosition,ClientHandle.EnemyPosition },
            {(int)ServerPackets.enemyHealth,ClientHandle.EnemyHealth }
        };
        Debug.Log("Initialized packets");
    }

    private void Disconnect()
    {
        isConnected = false;
        tcp.socket.Close();
        udp.socket.Close();

        Debug.Log("Disconnected from server.");
    }
}
