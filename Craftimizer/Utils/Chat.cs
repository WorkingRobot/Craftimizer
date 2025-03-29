using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;

namespace Craftimizer.Utils;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/0973b93931cdf8a1b01153984d62f76d998747ff/Utility/ChatHelper.cs#L17
public static unsafe class Chat
{
    public static void SendMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var str = Utf8String.FromString(message);
        try
        {
            ArgumentOutOfRangeException.ThrowIfZero(str->Length, nameof(message));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(str->Length, 500, nameof(message));

            var unsanitizedLength = str->Length;
            str->SanitizeString(
                AllowedEntities.Unknown9          | // 200
                AllowedEntities.Payloads          | //  40
                AllowedEntities.OtherCharacters   | //  20
                AllowedEntities.CharacterList     | //  10
                AllowedEntities.SpecialCharacters | //   8
                AllowedEntities.Numbers           | //   4
                AllowedEntities.LowercaseLetters  | //   2
                AllowedEntities.UppercaseLetters,   //   1
                null);
            ArgumentOutOfRangeException.ThrowIfNotEqual(unsanitizedLength, str->Length, nameof(message));

            UIModule.Instance()->ProcessChatBoxEntry(str);
        }
        finally
        {
            str->Dtor(true);
        }
    }
}
