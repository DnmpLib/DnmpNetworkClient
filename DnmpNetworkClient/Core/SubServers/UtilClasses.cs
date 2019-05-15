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
        Notification
    }

    internal enum WebGuiRequestType
    {
        Initialize,
        Disconnect
    }
}
