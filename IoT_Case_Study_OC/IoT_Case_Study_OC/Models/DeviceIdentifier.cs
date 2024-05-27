namespace Agent.Models
{
    public class DeviceIdentifier
    {
        public string DeviceName { get; set; }
        public string OpcNodeId { get; set; }
        public string AzureDeviceConnection { get; set; }

        public DeviceIdentifier(string deviceName, string opcNodeId, string azureDeviceConnection)
        {
            DeviceName = deviceName;
            OpcNodeId = opcNodeId;
            AzureDeviceConnection = azureDeviceConnection;
        }
    }
}
