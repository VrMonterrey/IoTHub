using Agent.Handlers;
using Agent.Models;
using Opc.UaFx.Client;
using System.Collections.Generic;

namespace Agent.Interfaces
{
    public interface IOpcManager
    {
        void Connect();
        void Disconnect();
        void SubscribeToNodeDataChange(string deviceName, List<OpcDataChangeHandlerMapper> changeHandlers);
        DeviceTelemetry? GetDeviceMetadata(string deviceName);
        void SetDeviceNodeData(string deviceName, string variableName, int newValue);
        void CallDeviceMethod(string deviceName, string methodName);
    }
}
