using IPC_MMF.Abstr;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace IPC_MMF.Impl
{
    public sealed class Ipc : IIpc
    {
        private Task? ReceiverWorker;
        private EventWaitHandle? NamedMMFWaiter;
        private MemoryMappedFile? MMF;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private readonly IpcMode WorkMode;
        private readonly int maxMessageLength;
        private readonly int maxMessageNumber;
        private readonly string name;
        private bool disposed = false;

        public event EventHandler<IpcEventArgs>? Received;

        public Ipc(string name)
        {
            this.name = name;
            WorkMode = IpcMode.Listener;
        }
        public Ipc(string name, int maxMessageLength, int maxMessageNumber) : this(name)
        {
            this.maxMessageNumber = maxMessageNumber;
            this.maxMessageLength = maxMessageLength;
            WorkMode = IpcMode.Owner;
        }

        public void Close()
        {
            cts.Cancel();
            NamedMMFWaiter?.Dispose();
            MMF?.Dispose();
            ReceiverWorker?.Dispose();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Close();
            }
        }

        public void Open()
        {
            Close();
            cts = new CancellationTokenSource();

            if (WorkMode == IpcMode.Listener)
            {
                OpenListen();
            }
            else if (WorkMode == IpcMode.Owner)
                OpenWrite();
        }

        private void OpenListen()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!EventWaitHandle.TryOpenExisting($"waiter_{name}", out NamedMMFWaiter))
                        throw new Exception("No system WaitHandle found");
                    MMF = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                    break;
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("No MMF found. Retrying...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                Thread.Sleep(1500);
            }
            Console.WriteLine($"Connected to MMF {name}");
            ReceiverWorker = Task.Factory.StartNew(() => WaitSignal(), cts.Token);
        }

        private void WaitSignal()
        {
            while (true)
            {
                if (NamedMMFWaiter is null || Received is null)
                    return;
                NamedMMFWaiter.WaitOne();
                var Data = ReadMMF();
                if (Data is not null)
                    OnReceived(Data);
            }
        }

        private IpcEventArgs? ReadMMF()
        {
            try
            {
                if (MMF is null)
                    return null;
                int MessageSize = 0, CommandSize = 0;
                string Command = string.Empty;
                using (var MMFStream = MMF.CreateViewStream(0, 5 + 128, MemoryMappedFileAccess.Read))
                {
                    Span<byte> Message = stackalloc byte[5 + 128];
                    MMFStream.Read(Message);
                    CommandSize = Message[0];
                    MessageSize = BitConverter.ToInt32(Message.Slice(1, 8));
                    Command = Encoding.UTF8.GetString(Message.Slice(5, CommandSize));
                }
                using (var MMFDataStream = MMF.CreateViewStream(5 + CommandSize, MessageSize, MemoryMappedFileAccess.Read))
                {
                    Span<byte> Message = stackalloc byte[MessageSize];
                    MMFDataStream.Read(Message);
                    var Data = MessageSize > 0 ? Message.Slice(0, MessageSize).ToArray() : null;
                    return new IpcEventArgs(Command, Data);
                }
            }
            catch
            {
                return null;
            }
        }

        private void OpenWrite()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting($"waiter_{name}", out NamedMMFWaiter))
                    NamedMMFWaiter = new EventWaitHandle(false, EventResetMode.AutoReset, $"waiter_{name}");
                var PersFile = File.Create("data.bin");
                MMF = MemoryMappedFile.CreateFromFile(PersFile, name, 5 + 128 + maxMessageLength * maxMessageNumber, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
                Console.WriteLine("Write session started");
            }
            catch 
            {
                Console.WriteLine("Error while starting write session");
            }
        }

        public void Send(string command)
        {
            if (WorkMode == IpcMode.Owner)
                SendFromServer(command, null);
        }

        public void Send(string command, byte[]? data)
        {
            if (WorkMode == IpcMode.Owner)
                SendFromServer(command, data);
        }

        private void SendFromServer(string command, byte[]? data)
        {
            if (MMF is null || NamedMMFWaiter is null)
                return;
            using (var MMFStream = MMF.CreateViewStream(0, 5 + 128 + maxMessageLength * maxMessageNumber))
            {
                Span<byte> CommandBytes = stackalloc byte[128];
                var CommandLength = Encoding.UTF8.GetBytes(command, CommandBytes);
                if (CommandLength > 128)
                    throw new Exception("Command is too big");
                MMFStream.Seek(0, SeekOrigin.Begin);
                MMFStream.WriteByte((byte)CommandLength);
                var DataSize = data is null ? new byte[] { 0 } : BitConverter.GetBytes(data.Length);
                MMFStream.Write(DataSize, 0, DataSize.Length);
                MMFStream.Seek(5, SeekOrigin.Begin);
                MMFStream.Write(CommandBytes.Slice(0, CommandLength));
                if (data is not null)
                    MMFStream.Write(data, 0, data.Length);
            }
            NamedMMFWaiter.Set();
        }

        private void OnReceived(IpcEventArgs arg)
        {
            if (Received is not null)
                Received(this, arg);
        }


        public sealed class IpcEventArgs : EventArgs
        {
            public IpcEventArgs(string command, byte[]? data) 
            { 
                Command = command;
                Data = data;
            }

            public string Command { get; set; }
            public byte[]? Data { get; set; }
        }
    }

    public enum IpcMode
    {
        Owner,
        Listener
    }
}
