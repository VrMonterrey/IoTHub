namespace Agent.Models
{
    internal class DeviceErrorMessage
    {
        public DeviceError DeviceError { get; set; }
        public uint NewErrorsCount { get; set; }

        public DeviceErrorMessage(DeviceError deviceError, uint newErrorsCount)
        {
            DeviceError = deviceError;
            NewErrorsCount = newErrorsCount;
        }
    }
}
