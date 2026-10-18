using System.Runtime.InteropServices;

namespace DynamicIsland.Services;

/// <summary>Reads the current Windows render-device peak without capturing or storing audio.</summary>
public sealed class AudioLevelService : IDisposable
{
    private static readonly Guid DeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private IAudioMeterInformation? _meter;

    public float GetPeakLevel()
    {
        try
        {
            EnsureMeter();
            return _meter is not null && _meter.GetPeakValue(out var peak) == 0
                ? Math.Clamp(peak, 0, 1)
                : 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ReleaseMeter();
            return 0;
        }
    }

    private void EnsureMeter()
    {
        if (_meter is not null) return;
        var enumeratorType = Type.GetTypeFromCLSID(DeviceEnumeratorClassId, throwOnError: true)!;
        _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
        Marshal.ThrowExceptionForHR(_enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out _device));
        var meterId = typeof(IAudioMeterInformation).GUID;
        Marshal.ThrowExceptionForHR(_device.Activate(ref meterId, CLSCTX.All, IntPtr.Zero, out var meter));
        _meter = (IAudioMeterInformation)meter;
    }

    private void ReleaseMeter()
    {
        ReleaseComObject(ref _meter);
        ReleaseComObject(ref _device);
        ReleaseComObject(ref _enumerator);
    }

    private static void ReleaseComObject<T>(ref T? value) where T : class
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        value = null;
    }

    public void Dispose()
    {
        ReleaseMeter();
        GC.SuppressFinalize(this);
    }

    private enum EDataFlow { Render, Capture, All }
    private enum ERole { Console, Multimedia, Communications }

    [Flags]
    private enum CLSCTX : uint
    {
        InprocServer = 0x1,
        InprocHandler = 0x2,
        LocalServer = 0x4,
        RemoteServer = 0x10,
        All = InprocServer | InprocHandler | LocalServer | RemoteServer
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, CLSCTX context, IntPtr activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }
}
