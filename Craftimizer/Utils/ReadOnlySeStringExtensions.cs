using Dalamud.Utility;
using Lumina.Text.ReadOnly;

namespace Craftimizer.Utils;

public static class ReadOnlySeStringExtensions
{
    public static string ExtractCleanText(this ReadOnlySeString self)
    {
        return self.ExtractText().StripSoftHyphen();
    }
}
