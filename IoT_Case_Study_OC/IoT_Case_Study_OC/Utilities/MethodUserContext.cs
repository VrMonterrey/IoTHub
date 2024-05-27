namespace Agent.Utilities
{
    internal class MethodUserContext
    {
        public string DeviceName { get; set; }

        public MethodUserContext(string deviceName)
        {
            DeviceName = deviceName;
        }
    }
}
