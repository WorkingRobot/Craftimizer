using Craftimizer.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;

namespace Craftimizer.Utils;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/0973b93931cdf8a1b01153984d62f76d998747ff/Utility/ChatHelper.cs#L17
public sealed unsafe class Chat
{
    private delegate void SendChatDelegate(UIModule* @this, Utf8String* message, Utf8String* historyMessage, bool pushToHistory);

   
    public Chat()
    {
        Service.GameInteropProvider.InitializeFromAttributes(this);
    }

    public void SendMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var str = Utf8String.FromString(message);
        str->SanitizeString(0x27F, null);
        //sendChat(UIModule.Instance(), str, null, false);
        str->Dtor(true);
    }
}
