using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using TcpMethods;
using System.IO;

namespace Network
{
    public class ServerTcp : Server
    {
        private TcpListener listener;   
        public int connectedClients {get => clients.Count;}
        private List<TcpClient> clients;      
        public Action<TcpClient> onClientClosed;
        public Action<TcpClient> onAccept;
        public bool listening {private set; get;}
        public int maxConnections;

#region Start
        
        public ServerTcp(int maxConnections, int bufferSize) : base(bufferSize, maxConnections)
        {
            this.maxConnections = maxConnections;
            clients = new List<TcpClient>();
        }

        public override async Task StartListening(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start(maxConnections);
            listening = true;
            await StartAccept(listener);
        }
#endregion

#region Accept
        private async Task StartAccept(TcpListener listener)
        {
            try
            {
                while(true)
                {
                    if(listening && connectedClients < maxConnections)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        Accept(client);
                    }
                }
            }
            catch (ObjectDisposedException){return;}
            catch(SocketException){return;}
        }

        private void Accept(TcpClient client)
        {
            clients.Add(client); 
            //Interlocked.Increment(ref connectedClients);   
            onAccept?.Invoke(client);
        }
#endregion

#region Receive
        
        public Task<ReceiveResult> ReceiveAsync(IPEndPoint ep)
        {
            return Tcp.ReceiveAsync(GetClient(ep), bufferSize, new CancellationToken());
        }
        public Task<ReceiveResult> ReceiveAsync(IPEndPoint ep, CancellationToken token)
        {
            return Tcp.ReceiveAsync(GetClient(ep), bufferSize, token);
        }

        public async Task<ReceiveResult> ReceiveAsync(TcpClient client)
        {
            if(client == null)
                return ReceiveResult.Failed();
            return await Tcp.ReceiveAsync(client, bufferSize, new CancellationToken());
        }
        
#endregion

#region Send
        public override void Send(byte[] buffer, IPEndPoint ep)
        {
            var client = GetClient(ep);
            if(client == null)
                throw new NullReferenceException("No client with that IPEndPoint exists");
            Tcp.Send(buffer, client, onSend);
        }

        public Task SendAsync(byte[] buffer, IPEndPoint ep)
        {
            var client = GetClient(ep);
            if(client == null)
                throw new NullReferenceException("No client with that IPEndPoint exists");
            return Tcp.SendAsync(buffer, client, onSend);
        }

        public void Send(byte[] buffer, TcpClient client)
        {
            if(client == null)
                return;
            Tcp.Send(buffer, client, onSend);
        }

        public Task SendFile(string file, IPEndPoint ep, long offset, long? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            return Tcp.SendFileAsync(file, GetClient(ep), bufferSize, onSend, offset, end, preBuffer, postBuffer);
        }
        
        public async Task SendFileToMultiple(string file, TcpClient[] clients, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            Task[] tasks = new Task[clients.Length];
            try
            {
                for(int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = Tcp.SendFileAsync(file, clients[i], bufferSize, onSend, offset, end, preBuffer, postBuffer);
                }
                await Task.WhenAll(tasks);
            }
            catch (SocketException){}
        }
        public Task SendFileToMultiple(string file, IPEndPoint[] ep, int offset, int? end, byte[] preBuffer = null, byte[] postBuffer = null)
        {
            TcpClient[] clients = new TcpClient[ep.Length];
            for(int i = 0; i < clients.Length; ++i)
                clients[i] = GetClient(ep[i]);
            return SendFileToMultiple(file, clients, offset, end, preBuffer, postBuffer);
        }
#endregion

#region Disconnect
        public void CloseClientSocket(TcpClient client)
        {
            try{
                client.Dispose();
                client.Close();

            }catch(Exception){} //Exception om socketen redan ??r st??ngd
            finally
            {
                if(client != null)
                {
                    //Interlocked.Decrement(ref connectedClients);
                    clients.Remove(client);
                    onClientClosed?.Invoke(client);
                }                
            }
        }

        public void StopListening()
        {
            listening = false;
            listener.Stop();
        }
        public override void Shutdown()
        {
            StopListening();
            foreach(var c in clients.ToArray())
            {
                CloseClientSocket(c);
            }
                
        }
#endregion

        public int ConnectedClients()
        {
            return connectedClients;
        }
        public TcpClient GetClient(IPEndPoint ep)
        {   
            if(ep == null)
                return null;
            
            var client = clients.Find(c => c?.Client?.RemoteEndPoint.Equals(ep) ?? false);
            return client;
        }
    }
}