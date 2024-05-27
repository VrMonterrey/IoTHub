namespace Agent.Models
{
    [Flags]
    public enum DeviceError
    {
        None = 0,
        EmergencyStop = 1,
        PowerFailure = 2,
        SensorFailure = 4,
        Unknown = 8
    }

    public static class DeviceErrorHelper
    {
        public static uint CountSetBits(this DeviceError flags)
        {
            uint count = 0;
            uint value = (uint)flags;

            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }
}
