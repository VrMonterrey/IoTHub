using Opc.UaFx.Client;

namespace Agent.Handlers
{
    public class OpcDataChangeHandlerMapper
    {
        public string OpcNodeDataName { get; }
        public OpcDataChangeReceivedEventHandler Handler { get; }

        public OpcDataChangeHandlerMapper(string opcNodeDataName, OpcDataChangeReceivedEventHandler handler)
        {
            OpcNodeDataName = opcNodeDataName;
            Handler = handler;
        }
    }
}
