using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using TplResult;

namespace TplSockets
{
    public static partial class TplSocketExtensions
    {
        public static async Task<Result> ConnectWithTimeoutAsync(
            this Socket socket,
            string remoteIpAddress,
            int port,
            int timeoutMs)
        {
            try
            {
                var connectTask = Task.Factory.FromAsync(
                    socket.BeginConnect,
                    socket.EndConnect,
                    remoteIpAddress,
                    port,
                    null);

                if (connectTask == await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    await connectTask.ConfigureAwait(false);
                }
                else
                {
                    throw new TimeoutException();
                }
            }
            catch (SocketException ex)
            {
                return Result.Fail($"{ex.Message} ({ex.GetType()})");
            }
            catch (TimeoutException ex)
            {
                return Result.Fail($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok();
        }

        public static async Task<Result<Socket>> AcceptAsync(this Socket socket)
        {
            Socket transferSocket;
            try
            {
                var acceptTask = Task<Socket>.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);
                transferSocket = await acceptTask.ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                return Result.Fail<Socket>($"{ex.Message} ({ex.GetType()})");
            }
            catch (InvalidOperationException ex)
            {
                return Result.Fail<Socket>($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok(transferSocket);
        }

        public static async Task<Result<int>> ReceiveWithTimeoutAsync(
            this Socket socket,
            byte[] buffer,
            int offset,
            int size,
            SocketFlags socketFlags,
            int timeoutMs)
        {
            int bytesReceived;
            try
            {
                var asyncResult = socket.BeginReceive(buffer, offset, size, socketFlags, null, null);
                var receiveTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));

                if (receiveTask == await Task.WhenAny(receiveTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    bytesReceived = await receiveTask.ConfigureAwait(false);
                }
                else
                {
                    throw new TimeoutException();
                }
            }
            catch (SocketException ex)
            {
                return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
            }
            catch (TimeoutException ex)
            {
                return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok(bytesReceived);
        }

        public static async Task<Result<int>> ReceiveAsync(
            this Socket socket,
            byte[] buffer,
            int offset,
            int size,
            SocketFlags socketFlags)
        {
            int bytesReceived;
            try
            {
                var asyncResult = socket.BeginReceive(buffer, offset, size, socketFlags, null, null);
                bytesReceived = await Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));
            }
            catch (SocketException ex)
            {
                return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok(bytesReceived);
        }

        public static async Task<Result<int>> SendWithTimeoutAsync(
            this Socket socket,
            byte[] buffer,
            int offset,
            int size,
            SocketFlags socketFlags,
            int timeoutMs)
        {
            int bytesSent;
            try
            {
                var asyncResult = socket.BeginSend(buffer, offset, size, socketFlags, null, null);
                var sendBytesTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndSend(asyncResult));

                if (sendBytesTask != await Task.WhenAny(sendBytesTask, Task.Delay(timeoutMs)).ConfigureAwait(false))
                {
                    throw new TimeoutException();
                }

                bytesSent = await sendBytesTask;
            }
            catch (SocketException ex)
            {
                return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
            }
            catch (TimeoutException ex)
            {
                return Result.Fail<int>($"{ex.Message} ({ex.GetType()})");
            }

            return Result.Ok(bytesSent);
        }
    }

}
