using Agent.Handlers;
using Agent.Interfaces;
using Agent.Models;
using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Agent.Services
{
    public class OpcManagerService : IOpcManager
    {
        private readonly Dictionary<string, string> _deviceNodeIds;
        private readonly OpcClient _client;
        private OpcSubscription _subscription;

        public OpcManagerService(string opcConnectionString, List<DeviceIdentifier> deviceIdentifierList)
        {
            _client = new OpcClient(opcConnectionString);
            _deviceNodeIds = new Dictionary<string, string>();

            foreach (var device in deviceIdentifierList)
            {
                _deviceNodeIds.Add(device.DeviceName, device.OpcNodeId);
            }
        }

        public void Connect()
        {
            _client.Connect();
            _subscription = _client.SubscribeNodes();
            _subscription.ChangeMonitoringMode(OpcMonitoringMode.Reporting);
        }

        public void Disconnect()
        {
            _subscription.ChangeMonitoringMode(OpcMonitoringMode.Disabled);
            _client.Disconnect();
        }

        private string GetDeviceOpcNodeId(string deviceName)
        {
            if (_deviceNodeIds.TryGetValue(deviceName, out var nodeId))
            {
                return nodeId;
            }

            throw new Exception($"No OPC connection for {deviceName}");
        }

        public void SubscribeToNodeDataChange(string deviceName, List<OpcDataChangeHandlerMapper> changeHandlers)
        {
            if (changeHandlers.Count == 0) return;

            var opcNodeId = GetDeviceOpcNodeId(deviceName);

            foreach (var changeHandler in changeHandlers)
            {
                var item = new OpcMonitoredItem($"{opcNodeId}/{changeHandler.OpcNodeDataName}", OpcAttribute.Value)
                {
                    Tag = deviceName
                };
                item.DataChangeReceived += changeHandler.Handler;

                _subscription.AddMonitoredItem(item);
            }

            _subscription.ApplyChanges();
        }

        public DeviceTelemetry? GetDeviceMetadata(string deviceName)
        {
            var properties = typeof(DeviceTelemetry).GetProperties();
            var commands = new List<OpcReadNode>();

            var opcNodeId = GetDeviceOpcNodeId(deviceName);

            foreach (var property in properties)
            {
                commands.Add(new OpcReadNode($"{opcNodeId}/{property.Name}"));
            }

            var data = _client.ReadNodes(commands.ToArray()).ToArray();

            if (data.Count() != properties.Length)
                throw new Exception("Mismatch between OPC data and DeviceTelemetry properties.");

            var metadata = new DeviceTelemetry(
                productionStatus: (int)data[0].Value,
                workorderId: (string)data[1].Value,
                goodCount: (long)data[2].Value,
                badCount: (long)data[3].Value,
                temperature: (double)data[4].Value);

            return metadata;
        }

        public void SetDeviceNodeData(string deviceName, string variableName, int newValue)
        {
            var opcNodeId = GetDeviceOpcNodeId(deviceName);
            var result = _client.WriteNode($"{opcNodeId}/{variableName}", newValue);

            if (!result.IsGood)
            {
                throw new Exception($"Cannot update {variableName} for {opcNodeId}");
            }
        }

        public void CallDeviceMethod(string deviceName, string methodName)
        {
            var opcNodeId = GetDeviceOpcNodeId(deviceName);
            _client.CallMethod(opcNodeId, $"{opcNodeId}/{methodName}");
        }
    }
}
