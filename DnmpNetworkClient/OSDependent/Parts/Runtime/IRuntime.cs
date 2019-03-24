namespace DnmpNetworkClient.OSDependant.Parts.Runtime
{
    internal interface IRuntime
    {
        void PreInit();
        void Init();
        void PostInit();
    }
}
