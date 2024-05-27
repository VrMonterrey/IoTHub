using Agent.Handlers;
using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Services
{
    public class DeviceManagerService : IDeviceManager
    {
        private readonly IOpcManager _opcManager;
        private readonly IIoTManager _iotManager;
        private readonly List<string> _deviceNames;
        private readonly Dictionary<string, DeviceError> _deviceErrors;
        private CancellationTokenSource _cancellationTokenSource;

        public DeviceManagerService(IOpcManager opcManager, IIoTManager iotManager, List<DeviceIdentifier> deviceIdentifiers)
        {
            _opcManager = opcManager;
            _iotManager = iotManager;
            _deviceNames = deviceIdentifiers.Select(d => d.DeviceName).ToList();
            _deviceErrors = new Dictionary<string, DeviceError>();
        }

        public async Task StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _opcManager.Connect();
            SubscribeToDesiredPropertiesChange();
            SubscribeToDataNodeChanges();
            SetDirectMethodsForAllDevices();
            SetDesiredProductionRateOnMachines();

            Console.WriteLine("Device Manager started");
            await PeriodicSendMetadataAsync(TimeSpan.FromSeconds(10));
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _opcManager.Disconnect();
            Console.WriteLine("Device Manager stopped");
        }

        private async Task PeriodicSendMetadataAsync(TimeSpan interval)
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                Console.WriteLine("Sending machines metadata...");
                await SendMachinesMetadataAsync();
                await Task.Delay(interval, _cancellationTokenSource.Token);
            }
        }

        private async Task SendMachinesMetadataAsync()
        {
            var tasks = new List<Task>();
            foreach (var deviceName in _deviceNames)
            {
                try
                {
                    var deviceTelemetry = _opcManager.GetDeviceMetadata(deviceName);
                    var dataString = JsonConvert.SerializeObject(deviceTelemetry);
                    var message = new Message(Encoding.UTF8.GetBytes(dataString))
                    {
                        ContentType = "application/json",
                        ContentEncoding = "utf-8"
                    };

                    Console.WriteLine($"Sending message from {deviceName}: {dataString}");

                    tasks.Add(_iotManager.SendMessageAsync(message, "Telemetry", deviceName));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error sending metadata for {deviceName}: {ex.Message}");
                }
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("Machines metadata sent");
        }


        private void SubscribeToDataNodeChanges()
        {
            var handlers = new List<OpcDataChangeHandlerMapper>
            {
                new OpcDataChangeHandlerMapper("ProductionRate", ProductionRateChangeHandler),
                new OpcDataChangeHandlerMapper("DeviceError", DeviceErrorChangeHandler)
            };

            foreach (var deviceName in _deviceNames)
            {
                try
                {
                    _opcManager.SubscribeToNodeDataChange(deviceName, handlers);
                    Console.WriteLine($"Subscribed to data node changes for {deviceName}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error subscribing to data node changes for {deviceName}: {ex.Message}");
                }
            }
        }

        private async void ProductionRateChangeHandler(object sender, OpcDataChangeReceivedEventArgs e)
        {
            var newValue = e.Item.Value.Value;
            var deviceName = ((OpcMonitoredItem)sender).Tag.ToString();
            try
            {
                await _iotManager.UpdateTwinReportedPropertyAsync(deviceName, "productionRate", newValue);
                Console.WriteLine($"Production rate updated for {deviceName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating production rate for {deviceName}: {ex.Message}");
            }
        }

        private async void DeviceErrorChangeHandler(object sender, OpcDataChangeReceivedEventArgs e)
        {
            var newValue = e.Item.Value.Value;
            var deviceName = ((OpcMonitoredItem)sender).Tag.ToString();

            if (deviceName == null) return;

            var currentError = (DeviceError)newValue;
            var previousError = _deviceErrors.GetValueOrDefault(deviceName, DeviceError.None);
            _deviceErrors[deviceName] = currentError;

            var newErrors = currentError & ~previousError;
            var newErrorsCount = newErrors.CountSetBits();

            var dataString = JsonConvert.SerializeObject(new DeviceErrorMessage(currentError, newErrorsCount));
            var message = new Message(Encoding.UTF8.GetBytes(dataString));

            try
            {
                await _iotManager.SendMessageAsync(message, "DeviceError", deviceName);
                await _iotManager.UpdateTwinReportedPropertyAsync(deviceName, "deviceErrors", newValue);
                Console.WriteLine($"Device error updated for {deviceName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating device error for {deviceName}: {ex.Message}");
            }
        }

        private void SetDirectMethodsForAllDevices()
        {
            var tasks = new List<Task>();

            foreach (var deviceName in _deviceNames)
            {
                tasks.Add(_iotManager.SetDirectMethodHandlerAsync(deviceName, "emergencyStop", CallEmergencyStop));
                tasks.Add(_iotManager.SetDirectMethodHandlerAsync(deviceName, "resetErrorStatus", CallResetErrorStatus));
            }

            try
            {
                Task.WhenAll(tasks).Wait();
                Console.WriteLine("Direct methods set for all devices");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error setting direct methods: {ex.Message}");
            }
        }

        private async Task<MethodResponse> CallEmergencyStop(MethodRequest methodRequest, object userContext)
        {
            var deviceName = ((MethodUserContext)userContext).DeviceName;
            try
            {
                await Task.Run(() => _opcManager.CallDeviceMethod(deviceName, "EmergencyStop"));
                Console.WriteLine($"Emergency stop called for {deviceName}");
                return new MethodResponse(200);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error calling emergency stop for {deviceName}: {ex.Message}");
                return new MethodResponse(500);
            }
        }

        private async Task<MethodResponse> CallResetErrorStatus(MethodRequest methodRequest, object userContext)
        {
            var deviceName = ((MethodUserContext)userContext).DeviceName;
            try
            {
                await Task.Run(() => _opcManager.CallDeviceMethod(deviceName, "ResetErrorStatus"));
                Console.WriteLine($"Reset error status called for {deviceName}");
                return new MethodResponse(200);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error calling reset error status for {deviceName}: {ex.Message}");
                return new MethodResponse(500);
            }
        }

        private void SubscribeToDesiredPropertiesChange()
        {
            var tasks = new List<Task>();
            foreach (var deviceName in _deviceNames)
            {
                tasks.Add(_iotManager.SetDesiredPropertyUpdateCallbackAsync(deviceName, OnDesiredPropertyChanged));
            }
            try
            {
                Task.WhenAll(tasks).Wait();
                Console.WriteLine("Subscribed to desired properties change for all devices");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error subscribing to desired properties change: {ex.Message}");
            }
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            var value = desiredProperties["productionRate"].Value;
            var deviceName = ((MethodUserContext)userContext).DeviceName;
            try
            {
                _opcManager.SetDeviceNodeData(deviceName, "ProductionRate", (int)value);
                Console.WriteLine($"Desired property changed for {deviceName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error changing desired property for {deviceName}: {ex.Message}");
            }
        }

        private async void SetDesiredProductionRateOnMachines()
        {
            foreach (var deviceName in _deviceNames)
            {
                try
                {
                    var desired = await _iotManager.GetTwinDesiredPropertiesAsync(deviceName);
                    if (!desired.Contains("productionRate")) continue;
                    var desiredProductionRate = desired["productionRate"];
                    _opcManager.SetDeviceNodeData(deviceName, "ProductionRate", (int)desiredProductionRate);
                    Console.WriteLine($"Desired production rate set for {deviceName}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error setting desired production rate for {deviceName}: {ex.Message}");
                }
            }
        }
    }
}
