using System.Threading.Tasks;

namespace Agent.Interfaces
{
    public interface IDeviceManager
    {
        Task StartAsync();
        void Stop();
    }
}
