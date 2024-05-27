using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.Threading.Tasks;

namespace Agent.Interfaces
{
    public interface IIoTManager
    {
        Task SendMessageAsync(Message message, string messageType, string deviceName);
        Task<TwinCollection> GetTwinDesiredPropertiesAsync(string deviceName);
        Task<TwinCollection> GetTwinReportedPropertiesAsync(string deviceName);
        Task UpdateTwinReportedPropertyAsync(string deviceName, string propertyName, dynamic value);
        Task SetDirectMethodHandlerAsync(string deviceName, string methodName, MethodCallback handler);
        Task SetDesiredPropertyUpdateCallbackAsync(string deviceName, DesiredPropertyUpdateCallback handler);
    }
}
