namespace DnmpNetworkClient.Core.LocalServers
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
