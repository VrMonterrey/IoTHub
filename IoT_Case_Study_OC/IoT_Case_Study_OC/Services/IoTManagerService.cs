using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;

namespace Agent.Services
{
    public class IoTManagerService : IIoTManager
    {
        private readonly string _connectionString;
        private readonly Dictionary<string, DeviceClient> _devices;

        public IoTManagerService(string connectionString, List<DeviceIdentifier> devices)
        {
            _connectionString = connectionString;
            _devices = new Dictionary<string, DeviceClient>();

            foreach (var device in devices)
            {
                var client = DeviceClient.CreateFromConnectionString(_connectionString + device.AzureDeviceConnection, TransportType.Mqtt);

                if (client == null)
                {
                    Console.Error.WriteLine($"DEVICE: {device.DeviceName} CAN'T BE RETRIEVED FROM AZURE");
                    continue;
                }

                _devices.Add(device.DeviceName, client);
            }
        }

        private DeviceClient GetDeviceClient(string deviceName)
        {
            if (_devices.TryGetValue(deviceName, out var client))
            {
                return client;
            }

            throw new Exception($"NO AZURE DEVICE CLIENT FOUND FOR {deviceName}");
        }

        public async Task SendMessageAsync(Message message, string messageType, string deviceName)
        {
            var deviceClient = GetDeviceClient(deviceName);

            message.ContentType = MediaTypeNames.Application.Json;
            message.ContentEncoding = "utf-8";
            message.Properties.Add("type", messageType);

            await deviceClient.SendEventAsync(message);
        }

        public async Task<TwinCollection> GetTwinDesiredPropertiesAsync(string deviceName)
        {
            var deviceClient = GetDeviceClient(deviceName);
            var twin = await deviceClient.GetTwinAsync();

            return twin.Properties.Desired;
        }

        public async Task<TwinCollection> GetTwinReportedPropertiesAsync(string deviceName)
        {
            var deviceClient = GetDeviceClient(deviceName);
            var twin = await deviceClient.GetTwinAsync();

            return twin.Properties.Reported;
        }

        public async Task UpdateTwinReportedPropertyAsync(string deviceName, string propertyName, dynamic value)
        {
            var deviceClient = GetDeviceClient(deviceName);

            var reportedProperties = new TwinCollection
            {
                [propertyName] = value
            };

            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        public async Task SetDirectMethodHandlerAsync(string deviceName, string methodName, MethodCallback handler)
        {
            var deviceClient = GetDeviceClient(deviceName);
            await deviceClient.SetMethodHandlerAsync(methodName, handler, new MethodUserContext(deviceName));
        }

        public async Task SetDesiredPropertyUpdateCallbackAsync(string deviceName, DesiredPropertyUpdateCallback handler)
        {
            var deviceClient = GetDeviceClient(deviceName);
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(handler, new MethodUserContext(deviceName));
        }
    }
}
