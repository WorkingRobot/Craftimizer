using Craftimizer.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;

namespace Craftimizer.Utils;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/0973b93931cdf8a1b01153984d62f76d998747ff/Utility/ChatHelper.cs#L17
public sealed unsafe class Chat
{
    private delegate void SendChatDelegate(UIModule* uiModule, Utf8String* message, Utf8String* historyMessage, bool pushToHistory);
    private delegate void SanitizeStringDelegate(Utf8String* data, int flags, Utf8String* buffer);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    private readonly SendChatDelegate sendChat = null!;

    [Signature("E8 ?? ?? ?? ?? EB 0A 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8D")]
    private readonly SanitizeStringDelegate sanitizeString = null!;

    public Chat()
    {
        Service.GameInteropProvider.InitializeFromAttributes(this);
    }

    public void SendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is empty", nameof(message));

        var str = Utf8String.FromString(message);
        sanitizeString(str, 0x27F, null);
        sendChat(Framework.Instance()->GetUiModule(), str, null, false);
        str->Dtor(true);
    }
}
