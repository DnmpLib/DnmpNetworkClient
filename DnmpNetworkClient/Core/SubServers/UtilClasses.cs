namespace DnmpNetworkClient.Core.SubServers
{
    internal enum WebGuiEventType
    {
        Initialization,
        SelfStatusChange,
        ClientConnect,
        ClientDisconnect,
        ClientUpdate,
        NetworkListUpdate,
    }

    internal enum WebGuiRequestType
    {
        Initialize,
        Disconnect
    }
}
