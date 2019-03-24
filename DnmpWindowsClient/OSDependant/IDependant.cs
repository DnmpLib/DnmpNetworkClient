using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DnmpWindowsClient.OSDependant.Parts.Gui;
using DnmpWindowsClient.OSDependant.Parts.Runtime;
using DnmpWindowsClient.OSDependant.Parts.Tap;

namespace DnmpWindowsClient.OSDependant
{
    internal interface IDependant
    {
        IGui GetGui();

        ITapInterface GetTapInerface();

        IRuntime GetRuntime();
    }
}
