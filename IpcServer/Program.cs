using IPC_MMF.Impl;

namespace IpcServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string[] cmds = new string[]
            {
                "create",
                "delete",
                "drop",
                "none",
                "calculate",
            };
            var Rnd = new Random();
            var IpcServer = new Ipc("testmmf", 8, 4);
            IpcServer.Open();
            while (true) 
            {
                var cmd = cmds[Rnd.Next(0, 5)];
                int msgsCnt = Rnd.Next(0, 4);
                var data = new byte[msgsCnt];
                Rnd.NextBytes(data);
                IpcServer.Send(cmd, data);
                Console.WriteLine($"{cmd} {String.Join(" ", data)} sent");
                Thread.Sleep(5000);
            }
        }
    }
}