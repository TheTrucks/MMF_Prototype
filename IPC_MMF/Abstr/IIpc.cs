using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace IPC_MMF.Abstr
{
    public interface IIpc : IDisposable
    {
        void Open();
        void Close();

        void Send(string command);
        void Send(string command, byte[]? data);
    }
}
