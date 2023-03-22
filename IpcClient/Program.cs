﻿using IPC_MMF.Impl;

namespace IpcClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var IpcServer = new Ipc("testmmf");
            IpcServer.Open();
            IpcServer.Received += Received;
            Console.ReadLine();
        }

        static void Received(object? sender, Ipc.IpcEventArgs e)
        {
            Console.WriteLine($"Command {e.Command}");
        }
    }
}