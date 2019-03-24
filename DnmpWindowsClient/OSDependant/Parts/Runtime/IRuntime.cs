namespace DnmpWindowsClient.OSDependant.Parts.Runtime
{
    internal interface IRuntime
    {
        void PreInit();
        void Init();
        void PostInit();
    }
}
