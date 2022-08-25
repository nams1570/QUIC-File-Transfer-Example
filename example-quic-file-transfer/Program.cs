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
        string endPath = @"C:\src\dir1\output.mxf";
        await ReceiveFile(stream, clientID, stopwatch, endPath);
    }
    private static async Task Client()
    {
        string path = @"C:\src\FCC_17_M10_POR_vs_NZL_UHD_TRIMMED.mxf";
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
            /*do
            {
                byte[] msgArr = new byte[1024 * 400];

                //read from file
                try
                {
                    read = await fsSource.ReadAsync(msgArr, 0, 1024 * 400);
                    totalRead += read;
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("Hi");
                }

                //send over quicconnection
                if (read != 0)
                {
                    Throttle(read, ref processed, maxBytesPerSecond, ref stopwatch, ref scheduler);
                    await stream.WriteAsync(msgArr, 0, read);
                }

                //if read == 0, then check if open file
                //set timeout loop
                else
                {
                    System.Console.WriteLine("Entering timeout loop");
                    while (timeoutSP > 0)
                    {
                        //Thread.Sleep(100);
                        await Task.Delay(100);
                        timeoutSP -= 100;
                        long end;
                        end = new FileInfo(filePath).Length;
                        System.Console.WriteLine($"file size in timeout is {end}");
                        if (end > size)
                        {
                            System.Console.WriteLine("Open file");
                            //send new filesize over stream, and change read so loop doesn't end
                            read = 1;
                            size = end;
                            //break when file size is different
                            break;
                        }

                    }
                    timeoutSP = timeout;
                    await SendMessage(stream, size.ToString());
                }

                //update signalR
                if (TimeSpan.Compare(stopwatch.Elapsed.Subtract(oldTime), TimeSpan.FromSeconds(update)) >= 0)
                {
                    _ = UpdateProgress(clientID, Status.TRANSFERRING, stopwatch.Elapsed, totalRead);
                    oldTime = stopwatch.Elapsed;
                }


            } while (read != 0);
            System.Console.WriteLine($"{totalRead} was read");

            _ = UpdateProgress(clientID, Status.SUCCESS, stopwatch.Elapsed);
            System.Console.WriteLine("Client: File sent");

            fsSource.Close();
        }
        catch (Exception e)
        {
            System.Console.Error.WriteLine("Client exception in SendFile:");
            System.Console.Error.WriteLine(e);
            _ = UpdateProgress(clientID, Status.ERROR, stopwatch.Elapsed);
            throw new Exception("Exception in SendFile");
        }*/
            byte[] msgArr = new byte[1024 * 1024];
            while ((read = await fsSource.ReadAsync(msgArr, 0, 1024*1024)) > 0)
            {
                totalRead += read;

                //Throttle(read, ref processed, maxBytesPerSecond, ref stopwatch, ref scheduler);
                await stream.WriteAsync(msgArr, 0, read);
                msgArr = null;
                msgArr = new byte[1024 * 1024];
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
        //CancellationTokenSource cts = new CancellationTokenSource(); 
        //cts.CancelAfter(1000 * 6);
        // Read message length
        System.Console.WriteLine($"[Server Connection #{clientID}] Reading message length.");
        long len = long.Parse(await ReceiveMessage(stream));
        
        FileStream fsNew = new FileStream(outputPath, FileMode.Append, FileAccess.Write);

        TimeSpan oldTime = stopwatch.Elapsed;

        // TODO make final / const variable that is set from settings?
        double UPDATE_INTERVAL = 1.0;

        long read = 0;

        //System.Console.WriteLine($"[Server Connection #{clientID}] Enter read loop.");
        try
        {
            /*while (len - read != 0)
            {
                byte[] buffer = new byte[1024 * 400];

                // Read 1024*1024 bytes unless there aren't enough bytes left
                int amountRead;
                if (len - read < 1024 * 400)
                {
                    amountRead = (int)(len - read);
                }
                else
                {
                    amountRead = 1024 * 400;
                }

                int temp = await stream.ReadAsync(buffer, 0, amountRead);

                read += temp;

                await fsNew.WriteAsync(buffer, 0, temp);

                // Update signalR 
                if (TimeSpan.Compare(stopwatch.Elapsed.Subtract(oldTime), TimeSpan.FromSeconds(UPDATE_INTERVAL)) >= 0)
                {
                    _ = UpdateProgress(clientID, Status.TRANSFERRING, stopwatch.Elapsed, outputPath, read);

                    oldTime = stopwatch.Elapsed;
                }


                // Check if filesize is increasing for open files
                if (len - read == 0)
                {
                    var res = await ReceiveMessage(stream);

                    len = long.Parse(res);
                }
            }
        } catch (Exception e)
        {
            System.Console.WriteLine($"[Server Connection #{clientID}] Error while reading file\n{e}");
        }*/
            byte[] buffer = new byte[1024*1024];
            int temp;
            while ((temp = await stream.ReadAsync(buffer, 0, 1024*1024)) > 0)
            {

                await fsNew.WriteAsync(buffer, 0, temp);
                buffer = null;
                // Update signalR 
                if (TimeSpan.Compare(stopwatch.Elapsed.Subtract(oldTime), TimeSpan.FromSeconds(UPDATE_INTERVAL)) >= 0)
                {

                    oldTime = stopwatch.Elapsed;
                }
                buffer = new byte[1024*1024];

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