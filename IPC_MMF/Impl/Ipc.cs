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

        private IpcMode WorkMode;
        private bool disposed = false;
        private int maxMessageLength;
        private int maxMessageNumber;
        private string name;

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
            ReceiverWorker?.Dispose();
            NamedMMFWaiter?.Dispose();
            MMF?.Dispose();
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
            if (WorkMode == IpcMode.Listener)
                OpenListen();
            else if (WorkMode == IpcMode.Owner)
                OpenWrite();
        }

        private void OpenListen()
        {
            while (!disposed)
            {
                try
                {
                    if (!EventWaitHandle.TryOpenExisting($"waiter_{name}", out NamedMMFWaiter))
                        throw new Exception("No system mutex found");
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
            }
            Console.WriteLine($"Connected to MMF {name}");
            ReceiverWorker = Task.Factory.StartNew(() => WaitSignal());
        }

        private void WaitSignal()
        {
            while (!disposed)
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
                using (var MMFStream = MMF.CreateViewStream(0, 128 + maxMessageLength * maxMessageNumber, MemoryMappedFileAccess.Read))
                {
                    Span<byte> Message = stackalloc byte[128 + maxMessageLength * maxMessageNumber];
                    MMFStream.Read(Message);
                    string Command = Encoding.UTF8.GetString(Message.Slice(0, 128));
                    var Data = Message.Slice(128).ToArray();
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
                MMF = MemoryMappedFile.CreateFromFile(PersFile, name, 128 + maxMessageLength * maxMessageNumber, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
                Console.WriteLine("Write session started");
            }
            catch 
            {
                Console.WriteLine("Error while starting write session");
            }
        }

        public void Send(string command)
        {
            if (WorkMode == IpcMode.Listener || MMF is null || NamedMMFWaiter is null)
                return;
            using (var MMFStream = MMF.CreateViewStream(0, 128))
            {
                var Cmd = Encoding.UTF8.GetBytes(command);
                if (Cmd.Length > 128)
                    throw new Exception("Command is too big");
                MMFStream.Seek(0, SeekOrigin.Begin);
                MMFStream.Write(Cmd);
            }
            NamedMMFWaiter.Set();
        }

        public void Send(string command, byte[]? data)
        {
            throw new NotImplementedException();
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
