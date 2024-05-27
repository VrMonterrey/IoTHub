namespace Agent.Models
{
    public class DeviceTelemetry
    {
        public int ProductionStatus { get; set; }
        public string WorkorderId { get; set; }
        public long GoodCount { get; set; }
        public long BadCount { get; set; }
        public double Temperature { get; set; }

        public DeviceTelemetry(int productionStatus, string workorderId, long goodCount, long badCount, double temperature)
        {
            ProductionStatus = productionStatus;
            WorkorderId = workorderId;
            GoodCount = goodCount;
            BadCount = badCount;
            Temperature = temperature;
        }
    }
}
