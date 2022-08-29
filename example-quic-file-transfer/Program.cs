// See https://aka.ms/new-console-template for more information
using System.Net.Quic;
using System.Text;
using System.Text.Json.Nodes;
using System.Drawing;
using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;
enum controlVariables
{ 
    BUFFER_SIZE = 1024 * 1024,
}

public class Program
{
    
    public static async Task Main(string[] args)
    {
        var serverTask = Server();
        var clientTask = Client();

        await clientTask;
        await serverTask;
    }
    private static async Task Server()
    {
        X509Store store = new X509Store(StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2 certificate = store.Certificates.FirstOrDefault(c => c.FriendlyName == "MsQuic-Test")
            ?? throw new Exception("Could not find 'MsQuic-Test' certificate");

        QuicListenerOptions options = new QuicListenerOptions
        {
            ListenEndPoint = IPEndPoint.Parse("0.0.0.0:5000"),
            ListenBacklog = 5,
            ApplicationProtocols =
                    new List<SslApplicationProtocol> { new SslApplicationProtocol("helloworld") },
            ConnectionOptionsCallback = (connection, clientHelloInfo, cancellationToken) =>
            {
                QuicServerConnectionOptions options = new QuicServerConnectionOptions
                {
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                    {
                        ApplicationProtocols =
                                new List<SslApplicationProtocol> { new SslApplicationProtocol("helloworld") },
                        ServerCertificate = certificate
                    },
                    DefaultStreamErrorCode = 1,
                    DefaultCloseErrorCode = 1,
                };
                return new ValueTask<QuicServerConnectionOptions>(options);
            }
        };

        QuicListener listener = await QuicListener.ListenAsync(options);
        System.Console.WriteLine("[Server] Listener created.");
        int clientID = 0;
        while (true)
        {
            try
            {
                QuicConnection connection = await listener.AcceptConnectionAsync();
                System.Console.WriteLine("[Server] Connection accepted.");

                _ = Task.Run(() => HandleClientConnection(connection, clientID++));

            }
            catch (Exception e)
            {
                System.Console.WriteLine("[Server] Exception occured:");
                System.Console.WriteLine(e);
            }
        }
    }
    private static async Task HandleClientConnection(QuicConnection connection, int clientID)
    {
        QuicStream stream = await connection.AcceptInboundStreamAsync();
        System.Console.WriteLine($"[Server Connection #{clientID}] Stream accepted.");
        // Start stopwatch
        ImmediateScheduler scheduler = Scheduler.Immediate;

        IStopwatch stopwatch = scheduler.StartStopwatch();
        string endPath = @"C:\src\dir1\output.mov";
        await ReceiveFile(stream, clientID, stopwatch, endPath);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static async Task Client()
    {
        string path = @"C:\src\file_example_MOV_480_700kB.mov";
        string conn = "127.0.0.1:5000";
        var options = new QuicClientConnectionOptions
        {
            RemoteEndPoint = System.Net.IPEndPoint.Parse(conn),
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols =
                       new List<SslApplicationProtocol> { new SslApplicationProtocol("helloworld") },
                RemoteCertificateValidationCallback =
                   (sender, certificate, chain, errors) => true // bypass remote certificate validation
            },
            DefaultStreamErrorCode = 1,
            DefaultCloseErrorCode = 1,
        };

        QuicConnection connection = await QuicConnection.ConnectAsync(options);

        System.Console.WriteLine("Client: connection created");
        ImmediateScheduler scheduler = Scheduler.Immediate;

        IStopwatch? stopwatch = scheduler.StartStopwatch();
        QuicStream stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
        System.Console.WriteLine($"[Client Connection #1] Stream opened.");
        await SendFile(stream, path, stopwatch, scheduler, -1);

    }
    private static async Task SendFile(QuicStream stream, string filePath, IStopwatch stopwatch, IScheduler scheduler, int maxBytesPerSecond)
    {
        //Open file functionality
        // Instead of reading all bytes, what do we do? 
        //msg is already a byte array so no need for msgBytes
        // Send message length 

        try
        {
            long size = new FileInfo(filePath).Length;

            await SendMessage(stream, size.ToString());

           


            int read = 0;
            long totalRead = 0;


            double update = 1.0;
            // Send message
            using FileStream fsSource = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            TimeSpan oldTime = stopwatch.Elapsed;
            
            byte[] msgArr = new byte[(int)controlVariables.BUFFER_SIZE];
            while ((read = await fsSource.ReadAsync(msgArr, 0, (int)controlVariables.BUFFER_SIZE)) > 0)
            {
                totalRead += read;

                await stream.WriteAsync(msgArr, 0, read);
                msgArr = null;
                msgArr = new byte[(int)controlVariables.BUFFER_SIZE];
                if (TimeSpan.Compare(stopwatch.Elapsed.Subtract(oldTime), TimeSpan.FromSeconds(update)) >= 0)
                {
                    oldTime = stopwatch.Elapsed;
                }

            }
            msgArr = null;
            fsSource.Close();
            fsSource.Close();

        }
        catch (Exception e)
        {
            System.Console.Error.WriteLine("Client exception in SendFile:");
            System.Console.Error.WriteLine(e);
            throw new Exception("Exception in SendFile");
        }

    }
    private static async Task ReceiveFile(QuicStream stream, double clientID, IStopwatch stopwatch, string outputPath)
    {
        
        // Read message length
        System.Console.WriteLine($"[Server Connection #{clientID}] Reading message length.");
        long len = long.Parse(await ReceiveMessage(stream));
        
        FileStream fsNew = new FileStream(outputPath, FileMode.Append, FileAccess.Write);

        TimeSpan oldTime = stopwatch.Elapsed;

        
        double UPDATE_INTERVAL = 1.0;

        long read = 0;

        try
        {
            byte[] buffer = new byte[(int)controlVariables.BUFFER_SIZE];
            int temp;
            while ((temp = await stream.ReadAsync(buffer, 0, (int)controlVariables.BUFFER_SIZE)) > 0)
            {

                await fsNew.WriteAsync(buffer, 0, temp);
                buffer = null;
                // Update signalR 
                if (TimeSpan.Compare(stopwatch.Elapsed.Subtract(oldTime), TimeSpan.FromSeconds(UPDATE_INTERVAL)) >= 0)
                {

                    oldTime = stopwatch.Elapsed;
                }
                buffer = new byte[(int)controlVariables.BUFFER_SIZE];

            }
            buffer = null;
            //System.Console.WriteLine($"[Server Connection #{clientID}] Exit read loop.");
            fsNew.Dispose();
            fsNew.Close();
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"[Server Connection #{clientID}] Error while reading file\n{e}");
        }
    }
    private static async Task SendMessage(QuicStream stream, string msg)
    {
        var msgBytes = Encoding.UTF8.GetBytes(msg);

        // Send message length (in 4 bytes)
        var len = BitConverter.GetBytes(msgBytes.Length);
        await stream.WriteAsync(len);

        // Send message
        await stream.WriteAsync(msgBytes);
    }

    private static async Task<string> ReceiveMessage(QuicStream stream)
    {
        var read = 0;

        // Read message length
        var lenBuffer = new byte[4];
        do
        {
            read += await stream.ReadAsync(lenBuffer, read, sizeof(int) - read);
        } while (read < sizeof(int));

        read = 0;

        var len = BitConverter.ToInt32(lenBuffer);

        // Read message
        var buffer = new byte[len];

        do
        {
            read = await stream.ReadAsync(buffer, read, len - read);
        } while (read < len);

        return Encoding.UTF8.GetString(buffer);
    }
}