using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Craftimizer.Plugin;

internal static unsafe class ImGuiExtras
{
    // https://github.com/ImGuiNET/ImGui.NET/blob/069363672fed940ebdaa02f9b032c282b66467c7/src/CodeGenerator/definitions/cimgui/definitions.json#L25394
    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe byte igInputTextEx(byte* label, byte* hint, byte* buf, int buf_size, Vector2 size, ImGuiInputTextFlags flags, ImGuiInputTextCallback? callback, void* user_data);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool igItemAdd(Vector4 bb, uint id, Vector4* navBb, uint flags);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool igButtonBehavior(Vector4 bb, uint id, bool* outHovered, bool* outHeld, ImGuiButtonFlags flags);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool igItemSize_Vec2(Vector2 size, float text_baseline_y = -1.0f);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern ImGuiItemFlags igGetItemFlags();

    // https://github.com/ImGuiNET/ImGui.NET/blob/069363672fed940ebdaa02f9b032c282b66467c7/src/ImGui.NET/Util.cs
    #region Util

    private const int StackAllocationSizeLimit = 2048;

    private static unsafe byte* Allocate(int byteCount) => (byte*)Marshal.AllocHGlobal(byteCount);

    private static unsafe void Free(byte* ptr) => Marshal.FreeHGlobal((IntPtr)ptr);

    private static unsafe int GetUtf8(ReadOnlySpan<char> s, byte* utf8Bytes, int utf8ByteCount)
    {
        if (s.IsEmpty)
            return 0;

        fixed (char* utf16Ptr = s)
            return Encoding.UTF8.GetBytes(utf16Ptr, s.Length, utf8Bytes, utf8ByteCount);
    }

    private static unsafe bool AreStringsEqual(byte* a, int aLength, byte* b)
    {
        for (var i = 0; i < aLength; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return b[aLength] == 0;
    }

    private static unsafe string StringFromPtr(byte* ptr)
    {
        var characters = 0;
        while (ptr[characters] != 0)
        {
            characters++;
        }

        return Encoding.UTF8.GetString(ptr, characters);
    }

    #endregion

    // Based off of code from InputTextWithHint: https://github.com/ImGuiNET/ImGui.NET/blob/069363672fed940ebdaa02f9b032c282b66467c7/src/ImGui.NET/ImGui.Manual.cs#L271
    public static unsafe bool InputTextEx(string label, string hint, ref string input, int maxLength, Vector2 size, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None, ImGuiInputTextCallback? callback = null, IntPtr user_data = default)
    {
        var utf8LabelByteCount = Encoding.UTF8.GetByteCount(label);
        byte* utf8LabelBytes;
        if (utf8LabelByteCount > StackAllocationSizeLimit)
        {
            utf8LabelBytes = Allocate(utf8LabelByteCount + 1);
        }
        else
        {
            var stackPtr = stackalloc byte[utf8LabelByteCount + 1];
            utf8LabelBytes = stackPtr;
        }
        GetUtf8(label, utf8LabelBytes, utf8LabelByteCount);

        var utf8HintByteCount = Encoding.UTF8.GetByteCount(hint);
        byte* utf8HintBytes;
        if (utf8HintByteCount > StackAllocationSizeLimit)
        {
            utf8HintBytes = Allocate(utf8HintByteCount + 1);
        }
        else
        {
            var stackPtr = stackalloc byte[utf8HintByteCount + 1];
            utf8HintBytes = stackPtr;
        }
        GetUtf8(hint, utf8HintBytes, utf8HintByteCount);
        
        var utf8InputByteCount = Encoding.UTF8.GetByteCount(input);
        var inputBufSize = Math.Max(maxLength + 1, utf8InputByteCount + 1);

        byte* utf8InputBytes;
        byte* originalUtf8InputBytes;
        if (inputBufSize > StackAllocationSizeLimit)
        {
            utf8InputBytes = Allocate(inputBufSize);
            originalUtf8InputBytes = Allocate(inputBufSize);
        }
        else
        {
            var inputStackBytes = stackalloc byte[inputBufSize];
            utf8InputBytes = inputStackBytes;
            var originalInputStackBytes = stackalloc byte[inputBufSize];
            originalUtf8InputBytes = originalInputStackBytes;
        }
        GetUtf8(input, utf8InputBytes, inputBufSize);
        var clearBytesCount = (uint)(inputBufSize - utf8InputByteCount);
        Unsafe.InitBlockUnaligned(utf8InputBytes + utf8InputByteCount, 0, clearBytesCount);
        Unsafe.CopyBlock(originalUtf8InputBytes, utf8InputBytes, (uint)inputBufSize);

        var result = igInputTextEx(
            utf8LabelBytes,
            utf8HintBytes,
            utf8InputBytes,
            inputBufSize,
            size,
            flags,
            callback,
            user_data.ToPointer());
        if (!AreStringsEqual(originalUtf8InputBytes, inputBufSize, utf8InputBytes))
        {
            input = StringFromPtr(utf8InputBytes);
        }

        if (utf8LabelByteCount > StackAllocationSizeLimit)
        {
            Free(utf8LabelBytes);
        }
        if (utf8HintByteCount > StackAllocationSizeLimit)
        {
            Free(utf8HintBytes);
        }
        if (inputBufSize > StackAllocationSizeLimit)
        {
            Free(utf8InputBytes);
            Free(originalUtf8InputBytes);
        }

        return result != 0;
    }

    public static unsafe bool ItemAdd(Vector4 bb, uint id, out Vector4 navBb, uint flags)
    {
        fixed (Vector4* navBbPtr = &navBb)
        {
            return igItemAdd(bb, id, navBbPtr, flags);
        }
    }

    public static unsafe bool ButtonBehavior(Vector4 bb, uint id, out bool hovered, out bool held, ImGuiButtonFlags flags)
    {
        fixed (bool* hoveredPtr = &hovered)
        fixed (bool* heldPtr = &held)
        {
            return igButtonBehavior(bb, id, hoveredPtr, heldPtr, flags);
        }
    }

    public static unsafe bool ItemSize(Vector2 size, float text_baseline_y = -1.0f) =>
        igItemSize_Vec2(size, text_baseline_y);

    public static unsafe ImGuiItemFlags GetItemFlags() =>
        igGetItemFlags();

    public static unsafe bool SetDragDropPayload<T>(string type, T data) where T : unmanaged =>
        ImGui.SetDragDropPayload(type, (nint)(&data), (uint)sizeof(T));

    public static unsafe bool AcceptDragDropPayload<T>(string type, out T data) where T : unmanaged
    {
        var payload = ImGui.AcceptDragDropPayload(type);
        if (payload.NativePtr == null || payload.DataSize != sizeof(T))
        {
            data = default;
            return false;
        }
        data = *(T*)payload.Data;
        return true;
    }
}

// https://github.com/ocornut/imgui/blob/v1.88/imgui_internal.h#L758
[Flags]
internal enum ImGuiItemFlags
{
    None = 0,
    NoTabStop = 1 << 0, // Disable keyboard tabbing. This is a "lighter" version of ImGuiItemFlags_NoNav.
    ButtonRepeat = 1 << 1, // Button() will return true multiple times based on io.KeyRepeatDelay and io.KeyRepeatRate settings.
    Disabled = 1 << 2, // Disable interactions but doesn't affect visuals. See BeginDisabled()/EndDisabled(). See github.com/ocornut/imgui/issues/211
    NoNav = 1 << 3, // Disable any form of focusing (keyboard/gamepad directional navigation and SetKeyboardFocusHere() calls)
    NoNavDefaultFocus = 1 << 4, // Disable item being a candidate for default focus (e.g. used by title bar items)
    SelectableDontClosePopup = 1 << 5, // Disable MenuItem/Selectable() automatically closing their popup window
    MixedValue = 1 << 6, // [BETA] Represent a mixed/indeterminate value, generally multi-selection where values differ. Currently only supported by Checkbox() (later should support all sorts of widgets)
    ReadOnly = 1 << 7, // [ALPHA] Allow hovering interactions but underlying value is not changed.
    Inputable = 1 << 8, // [WIP] Auto-activate input mode when tab focused. Currently only used and supported by a few items before it becomes a generic feature.
}
