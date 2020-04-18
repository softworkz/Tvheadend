namespace TVHeadEnd.HTSP
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Model.Logging;

    using TVHeadEnd.Helper;
    using TVHeadEnd.HTSP.Responses;

    public class HtsConnection
    {
        private const long BytesPerGiga = 1024 * 1024 * 1024;

        private readonly ByteList buffer;
        private readonly string clientName;
        private readonly string clientVersion;

        private readonly SemaphoreSlim lockObj = new SemaphoreSlim(1, 1);
        private readonly ILogger logger;
        private readonly CancellationTokenSource messageBuilderThreadTokenSource;
        private readonly CancellationTokenSource messageDistributorThreadTokenSource;
        private readonly SizeQueue<HtsMessage> messagesForSendQueue;
        private readonly SizeQueue<HtsMessage> receivedMessagesQueue;

        private readonly CancellationTokenSource receiveHandlerThreadTokenSource;
        private readonly Dictionary<int, IHtsResponseHandler> responseHandlers;
        private readonly CancellationTokenSource sendingHandlerThreadTokenSource;
        private Thread messageBuilderThread;
        private Thread messageDistributorThread;
        private Thread receiveHandlerThread;
        private Thread sendingHandlerThread;

        private volatile int seq;
        private Socket socket;

        private HtsMessage helloResponse;

        public event EventHandler<HtsMessage> MessageReceived;

        public event EventHandler<Exception> ConnectionError;

        public HtsConnection(string clientName, string clientVersion, ILogger logger)
        {
            this.logger = logger;

            this.IsConnected = false;

            this.clientName = clientName;
            this.clientVersion = clientVersion;

            this.buffer = new ByteList();
            this.receivedMessagesQueue = new SizeQueue<HtsMessage>(int.MaxValue);
            this.messagesForSendQueue = new SizeQueue<HtsMessage>(int.MaxValue);
            this.responseHandlers = new Dictionary<int, IHtsResponseHandler>();

            this.receiveHandlerThreadTokenSource = new CancellationTokenSource();
            this.messageBuilderThreadTokenSource = new CancellationTokenSource();
            this.sendingHandlerThreadTokenSource = new CancellationTokenSource();
            this.messageDistributorThreadTokenSource = new CancellationTokenSource();
            this.ServerInfo = new HtsServerInfo();
        }

        public bool IsConnected { get; private set; }

        public bool IsAuthenticated { get; private set; }

        public bool Authenticate(string username, string password)
        {
            this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: start");

            this.IsAuthenticated = false;

            if (this.helloResponse == null)
            {
                this.helloResponse = this.SendHello(username);
            }

            if (this.helloResponse == null)
            {
                this.logger.Error("[TVHclient] HTSConnectionAsync.authenticate: no hello response");
                return false;
            }

            byte[] salt;
            if (this.helloResponse.ContainsField("challenge"))
            {
                salt = this.helloResponse.GetByteArray("challenge");
            }
            else
            {
                salt = new byte[0];
                this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: hello don't deliver required field 'challenge' - htsp wrong implemented on tvheadend side.");
            }

            var loopBackResponseHandler = new LoopBackResponseHandler();

            byte[] digest = Sha1Helper.GenerateSaltedSha1(password, salt);
            HtsMessage authMessage = new HtsMessage();
            authMessage.Method = "authenticate";
            authMessage.PutField("username", username);
            authMessage.PutField("digest", digest);
            this.SendMessage(authMessage, loopBackResponseHandler);
            HtsMessage authResponse = loopBackResponseHandler.GetResponse();
            if (authResponse != null)
            {
                bool auth = authResponse.GetInt("noaccess", 0) != 1;
                if (auth)
                {
                    this.IsAuthenticated = true;

                    HtsMessage getDiskSpaceMessage = new HtsMessage();
                    getDiskSpaceMessage.Method = "getDiskSpace";
                    this.SendMessage(getDiskSpaceMessage, loopBackResponseHandler);
                    HtsMessage diskSpaceResponse = loopBackResponseHandler.GetResponse();
                    if (diskSpaceResponse != null)
                    {
                        long freeDiskSpace = -1;
                        long totalDiskSpace = -1;
                        if (diskSpaceResponse.ContainsField("freediskspace"))
                        {
                            freeDiskSpace = diskSpaceResponse.GetLong("freediskspace") / BytesPerGiga;
                        }
                        else
                        {
                            this.logger.Info(
                                "[TVHclient] HTSConnectionAsync.authenticate: getDiskSpace don't deliver required field 'freediskspace' - htsp wrong implemented on tvheadend side.");
                        }

                        if (diskSpaceResponse.ContainsField("totaldiskspace"))
                        {
                            totalDiskSpace = diskSpaceResponse.GetLong("totaldiskspace") / BytesPerGiga;
                        }
                        else
                        {
                            this.logger.Info(
                                "[TVHclient] HTSConnectionAsync.authenticate: getDiskSpace don't deliver required field 'totaldiskspace' - htsp wrong implemented on tvheadend side.");
                        }

                        this.ServerInfo.Diskspace = freeDiskSpace + "GB / " + totalDiskSpace + "GB";
                    }

                    HtsMessage enableAsyncMetadataMessage = new HtsMessage();
                    enableAsyncMetadataMessage.Method = "enableAsyncMetadata";
                    enableAsyncMetadataMessage.PutField("epg", 0);
                    this.SendMessage(enableAsyncMetadataMessage, null);
                }

                this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: authenticated: " + auth);
                return auth;
            }

            return false;
        }

        private HtsMessage SendHello(string username)
        {
            HtsMessage helloMessage = new HtsMessage();
            helloMessage.Method = "hello";
            helloMessage.PutField("clientname", this.clientName);
            helloMessage.PutField("clientversion", this.clientVersion);
            helloMessage.PutField("htspversion", HtsMessage.HTSP_VERSION);
            helloMessage.PutField("username", username);

            var loopBackResponseHandler = new LoopBackResponseHandler();
            this.SendMessage(helloMessage, loopBackResponseHandler);
            var response = loopBackResponseHandler.GetResponse();
            if (response != null)
            {
                if (response.ContainsField("htspversion"))
                {
                    this.ServerInfo.ServerProtocolVersion = response.GetInt("htspversion");
                }
                else
                {
                    this.ServerInfo.ServerProtocolVersion = -1;
                    this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: hello don't deliver required field 'htspversion' - htsp wrong implemented on tvheadend side.");
                }

                if (response.ContainsField("servername"))
                {
                    this.ServerInfo.Servername = response.GetString("servername");
                }
                else
                {
                    this.ServerInfo.Servername = "n/a";
                    this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: hello don't deliver required field 'servername' - htsp wrong implemented on tvheadend side.");
                }

                if (response.ContainsField("serverversion"))
                {
                    this.ServerInfo.Serverversion = response.GetString("serverversion");
                }
                else
                {
                    this.ServerInfo.Serverversion = "n/a";
                    this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: hello don't deliver required field 'serverversion' - htsp wrong implemented on tvheadend side.");
                }

                if (response.ContainsField("webroot"))
                {
                    this.ServerInfo.WebRoot = response.GetString("webroot");
                }
                else
                {
                    this.logger.Info("[TVHclient] HTSConnectionAsync.authenticate: hello don't deliver required field 'webroot' - htsp wrong implemented on tvheadend side.");
                }
            }

            return response;
        }

        public bool NeedsRestart { get; private set; }

        public HtsServerInfo ServerInfo { get; }

        public async Task<bool> Open(string hostname, int port, string username, int retries, CancellationToken cancellationToken)
        {
            if (this.IsConnected)
            {
                return true;
            }

            await this.lockObj.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

            if (this.IsConnected)
            {
                return true;
            }

            try
            {
                // Establish the remote endpoint for the socket.
                IPAddress ipAddress;
                if (!IPAddress.TryParse(hostname, out ipAddress))
                {
                    // no IP --> ask DNS
                    IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(hostname).ConfigureAwait(false);
                    ipAddress = ipHostInfo.AddressList[0];
                }

                int i = 0;
                while (!this.IsConnected && i++ <= retries && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        IPEndPoint remoteEp = new IPEndPoint(ipAddress, port);

                        this.logger.Info(
                            "[TVHclient] HTSConnectionAsync.open: " +
                            "IPEndPoint = '" + remoteEp + "'; " +
                            "AddressFamily = '" + ipAddress.AddressFamily + "'");

                        // Create a TCP/IP  socket.
                        this.socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                        // connect to server
                        await this.socket.ConnectAsync(remoteEp).ConfigureAwait(false);

                        this.IsConnected = true;
                        this.logger.Info("[TVHclient] HTSConnectionAsync.open: socket connected.");

                        ThreadStart receiveHandlerRef = this.ReceiveHandler;
                        this.receiveHandlerThread = new Thread(receiveHandlerRef);
                        this.receiveHandlerThread.IsBackground = true;
                        this.receiveHandlerThread.Start();

                        ThreadStart messageBuilderRef = this.MessageBuilder;
                        this.messageBuilderThread = new Thread(messageBuilderRef);
                        this.messageBuilderThread.IsBackground = true;
                        this.messageBuilderThread.Start();

                        ThreadStart sendingHandlerRef = this.SendingHandler;
                        this.sendingHandlerThread = new Thread(sendingHandlerRef);
                        this.sendingHandlerThread.IsBackground = true;
                        this.sendingHandlerThread.Start();

                        ThreadStart messageDistributorRef = this.MessageDistributor;
                        this.messageDistributorThread = new Thread(messageDistributorRef);
                        this.messageDistributorThread.IsBackground = true;
                        this.messageDistributorThread.Start();

                        this.helloResponse = this.SendHello(username);

                        return true;
                    }
                    catch (Exception ex)
                    {
                        this.logger.Error("[TVHclient] HTSConnectionAsync.open: caught exception : {0}", ex.Message);

                        if (retries == 0)
                        {
                            throw;
                        }

                        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                this.lockObj.Release();
            }

            return false;
        }

        public void SendMessage(HtsMessage message, IHtsResponseHandler responseHandler)
        {
            // loop the sequence number
            if (this.seq == int.MaxValue)
            {
                this.seq = int.MinValue;
            }
            else
            {
                this.seq++;
            }

            // housekeeping verry old response handlers
            if (this.responseHandlers.ContainsKey(this.seq))
            {
                this.responseHandlers.Remove(this.seq);
            }

            message.PutField("seq", this.seq);
            this.messagesForSendQueue.Enqueue(message);
            this.responseHandlers.Add(this.seq, responseHandler);
        }

        public void Stop()
        {
            try
            {
                if (this.receiveHandlerThread?.IsAlive == true)
                {
                    this.receiveHandlerThreadTokenSource.Cancel();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                if (this.messageBuilderThread?.IsAlive == true)
                {
                    this.messageBuilderThreadTokenSource.Cancel();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                if (this.sendingHandlerThread?.IsAlive == true)
                {
                    this.sendingHandlerThreadTokenSource.Cancel();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                if (this.messageDistributorThread?.IsAlive == true)
                {
                    this.messageDistributorThreadTokenSource.Cancel();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                if (this.socket?.Connected == true)
                {
                    this.socket.Close();
                }
            }
            catch
            {
                // ignored
            }

            this.NeedsRestart = true;
            this.IsConnected = false;
        }

        private void MessageBuilder()
        {
            bool threadOk = true;
            while (this.IsConnected && threadOk)
            {
                if (this.messageBuilderThreadTokenSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    byte[] lengthInformation = this.buffer.GetFromStart(4);
                    long messageDataLength = HtsMessage.UIntToLong(lengthInformation[0], lengthInformation[1], lengthInformation[2], lengthInformation[3]);
                    byte[] messageData = this.buffer.ExtractFromStart((int)messageDataLength + 4); // should be long !!!
                    HtsMessage response = HtsMessage.Parse(messageData, this.logger);
                    this.receivedMessagesQueue.Enqueue(response);
                }
                catch (Exception ex)
                {
                    threadOk = false;

                    this.ConnectionError?.Invoke(this, ex);

                    if (this.ConnectionError == null)
                    {
                        this.logger.ErrorException("[TVHclient] MessageBuilder caught exception : {0} but no error listener is configured!!!", ex, ex.ToString());
                    }
                }
            }
        }

        private void MessageDistributor()
        {
            bool threadOk = true;
            while (this.IsConnected && threadOk)
            {
                if (this.messageDistributorThreadTokenSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    HtsMessage response = this.receivedMessagesQueue.Dequeue();
                    if (response.ContainsField("seq"))
                    {
                        int seqNo = response.GetInt("seq");
                        if (this.responseHandlers.ContainsKey(seqNo))
                        {
                            IHtsResponseHandler currHtsResponseHandler = this.responseHandlers[seqNo];
                            if (currHtsResponseHandler != null)
                            {
                                this.responseHandlers.Remove(seqNo);
                                currHtsResponseHandler.HandleResponse(response);
                            }
                        }
                        else
                        {
                            this.logger.Fatal("[TVHclient] MessageDistributor: HTSResponseHandler for seq = '" + seqNo + "' not found!");
                        }
                    }
                    else
                    {
                        // auto update messages
                        this.MessageReceived?.Invoke(this, response);
                    }
                }
                catch (Exception ex)
                {
                    threadOk = false;

                    this.ConnectionError?.Invoke(this, ex);

                    if (this.ConnectionError == null)
                    {
                        this.logger.ErrorException("[TVHclient] MessageBuilder caught exception : {0} but no error listener is configured!!!", ex, ex.ToString());
                    }
                }
            }
        }

        private void ReceiveHandler()
        {
            bool threadOk = true;
            byte[] readBuffer = new byte[1024];
            while (this.IsConnected && threadOk)
            {
                if (this.receiveHandlerThreadTokenSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    int bytesReveived = this.socket.Receive(readBuffer);
                    this.buffer.AppendCount(readBuffer, bytesReveived);
                }
                catch (Exception ex)
                {
                    threadOk = false;
                    this.ConnectionError?.Invoke(this, ex);

                    if (this.ConnectionError == null)
                    {
                        this.logger.ErrorException("[TVHclient] ReceiveHandler caught exception : {0} but no error listener is configured!!!", ex, ex.ToString());
                    }
                }
            }
        }

        private void SendingHandler()
        {
            bool threadOk = true;
            while (this.IsConnected && threadOk)
            {
                if (this.sendingHandlerThreadTokenSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    HtsMessage message = this.messagesForSendQueue.Dequeue();
                    byte[] data2Send = message.BuildBytes();
                    int bytesSent = this.socket.Send(data2Send);
                    if (bytesSent != data2Send.Length)
                    {
                        this.logger.Error(
                            "[TVHclient] SendingHandler: Sending not complete! \nBytes sent: " + bytesSent + "\nMessage bytes: " +
                            data2Send.Length + "\nMessage: " + message);
                    }
                }
                catch (Exception ex)
                {
                    threadOk = false;
                    this.logger.Error("[TVHclient] SendingHandler caught exception : {0}", ex.ToString());

                    this.ConnectionError?.Invoke(this, ex);

                    if (this.ConnectionError == null)
                    {
                        this.logger.ErrorException("[TVHclient] SendingHandler caught exception : {0} but no error listener is configured!!!", ex, ex.ToString());
                    }
                }
            }
        }
    }
}