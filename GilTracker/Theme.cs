using System.Linq;
using System.Numerics;

namespace GilTracker;

/// <summary>A color palette for the window. Both plugins ship the same set so they can match.</summary>
public sealed record Theme(
    string Name,
    uint Background, uint Surface, uint SurfaceHover, uint SurfacePress, uint Line,
    uint Header, uint Border, uint Text, uint TextSoft,
    uint Title, uint Accent, uint OnAccent,
    uint Star, uint StarLight, uint Sticker, uint StickerRim,
    uint Card, uint CardEdge, uint CardDeep, uint Good, uint Warn,
    uint Card2, uint Card2Edge, uint Bad,
    uint Money, uint MoneyPill, uint OnMoneyPill, uint Coin, uint CoinLight, uint CoinDeep)
{
    public const string DefaultName = "Sticker Book";

    public static readonly Theme[] All =
    [
        // Khloe's journal: parchment, coral, mint.
        new("Sticker Book",
            0xF7EDD6, 0xEADBB9, 0xE2CC9F, 0xDBC28F, 0xCDAE78,
            0xD8BC86, 0xB88E55, 0x4A3222, 0x86694F,
            0xD2553A, 0xD2553A, 0xFFFFFF,
            0xD9A54A, 0xE9C57A, 0xE8873A, 0xFFF6E4,
            0xDDF1EC, 0x8CC3B8, 0xC7E6DF, 0x2E7D72, 0xB86E14,
            0xFBE4D8, 0xE3A58E, 0xC4502F,
            0x8F6210, 0xB07E22, 0xFFFFFF, 0xD9A54A, 0xF2D28A, 0xB07E22),

        // Night sky indigo with gold.
        new("Starlit",
            0x17142A, 0x2A2444, 0x342D52, 0x3D3560, 0x4A4166,
            0x221D38, 0x3A3356, 0xEDE8F7, 0xA79FC2,
            0xF3CD7A, 0xE2B04A, 0x1B1730,
            0xE2B04A, 0xF3CD7A, 0xE2B04A, 0xFFF3D4,
            0x241F3B, 0xE2B04A, 0x2F2948, 0x7FD8C8, 0xF2B58F,
            0x2A1F2E, 0x8A5640, 0xF08A6C,
            0xF3CD7A, 0xE2B04A, 0x1B1730, 0xE2B04A, 0xF3CD7A, 0xB8862C),

        // Soft pinks and lilac, kupo.
        new("Moogle Post",
            0xFFF1F5, 0xF8DCE6, 0xF2CAD8, 0xEBB8CB, 0xE2A5BC,
            0xF5C6D6, 0xD987A6, 0x4B2B3F, 0x8E6A80,
            0xC0487A, 0xC0487A, 0xFFFFFF,
            0xE9B949, 0xF7DE97, 0xD9536F, 0xFFFFFF,
            0xEAE4FB, 0xA898DA, 0xDCD3F6, 0x5B4BAA, 0xB5651D,
            0xFFE6DA, 0xE8A48C, 0xC2412B,
            0x8F5A12, 0xC0487A, 0xFFFFFF, 0xE9B949, 0xF7DE97, 0xB8862C),

        // Close to the game's own dark UI.
        new("Eorzea Dark",
            0x1E1E1E, 0x2D2B28, 0x3A3733, 0x45413B, 0x5A544A,
            0x2A2723, 0x8C7A52, 0xEEE6D2, 0xA99F8A,
            0xE8CC8A, 0xC9A55A, 0x1E1A12,
            0xD6B26A, 0xF0DCA8, 0xD6B26A, 0xF5EBD2,
            0x26302E, 0x6FA79A, 0x2F3B38, 0x8FD3BF, 0xE6B06A,
            0x332622, 0xA8705A, 0xEE8A6A,
            0xE8CC8A, 0xC9A55A, 0x1E1A12, 0xD6B26A, 0xF0DCA8, 0xA88A45),

        // Cool Coerthas blues.
        new("Ishgard Frost",
            0xEEF4F8, 0xDCE7EF, 0xCFDDE8, 0xC1D2E0, 0xA9BFD0,
            0xC9D9E6, 0x8AA6BC, 0x1F3344, 0x5E7488,
            0x2F6E9E, 0x2F6E9E, 0xFFFFFF,
            0xC9A24A, 0xEAD394, 0x4C8DBF, 0xFFFFFF,
            0xE3F1EC, 0x8FC1B2, 0xD2E8E0, 0x2D7564, 0xA86412,
            0xF6E7E1, 0xD9A895, 0xB4472C,
            0x7A5A12, 0x2F6E9E, 0xFFFFFF, 0xC9A24A, 0xEAD394, 0x9A7A2A),
    ];

    public static Theme Get(string? name) => All.FirstOrDefault(t => t.Name == name) ?? All[0];

    public static Vector4 V(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        1f);
}
