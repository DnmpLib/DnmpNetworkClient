namespace DnmpNetworkClient.OSDependent.Parts.Runtime
{
    internal interface IRuntime
    {
        void PreInit(bool useGui);
        void PostInit(bool useGui);
    }
}
