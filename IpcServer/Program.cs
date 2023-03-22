using IPC_MMF.Impl;

namespace IpcServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string[] cmds = new string[]
            {
                "qwer",
                "asdf",
                "zxcv",
                "uiop",
                "hjkl"
            };
            var Rnd = new Random();
            var IpcServer = new Ipc("testmmf", 128, 4);
            IpcServer.Open();
            while (true) 
            {
                var cmd = cmds[Rnd.Next(0, 5)];
                IpcServer.Send(cmd);
                Console.WriteLine($"{cmd} sent");
                Thread.Sleep(1000);
            }
        }
    }
}