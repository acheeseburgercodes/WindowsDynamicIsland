using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynamicIsland.Services;

public static class ColorPickerService
{
    private const uint CcRgbInit = 0x00000001;
    private const uint CcFullOpen = 0x00000002;
    private static readonly int[] CustomColors = new int[16];

    public static bool TryPick(Window owner, Color initialColor, out Color selectedColor)
    {
        var customColorsPointer = Marshal.AllocCoTaskMem(CustomColors.Length * sizeof(int));
        try
        {
            Marshal.Copy(CustomColors, 0, customColorsPointer, CustomColors.Length);
            var dialog = new ChooseColorData
            {
                StructSize = Marshal.SizeOf<ChooseColorData>(),
                Owner = new WindowInteropHelper(owner).Handle,
                ResultColor = ToColorRef(initialColor),
                CustomColors = customColorsPointer,
                Flags = CcRgbInit | CcFullOpen
            };

            if (!ChooseColor(ref dialog))
            {
                selectedColor = initialColor;
                return false;
            }

            Marshal.Copy(customColorsPointer, CustomColors, 0, CustomColors.Length);
            selectedColor = FromColorRef(dialog.ResultColor);
            return true;
        }
        finally
        {
            Marshal.FreeCoTaskMem(customColorsPointer);
        }
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private static Color FromColorRef(int colorRef) => Color.FromRgb(
        (byte)(colorRef & 0xFF),
        (byte)((colorRef >> 8) & 0xFF),
        (byte)((colorRef >> 16) & 0xFF));

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChooseColor(ref ChooseColorData chooseColor);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ChooseColorData
    {
        public int StructSize;
        public nint Owner;
        public nint Instance;
        public int ResultColor;
        public nint CustomColors;
        public uint Flags;
        public nint CustomData;
        public nint Hook;
        public string? TemplateName;
    }
}
