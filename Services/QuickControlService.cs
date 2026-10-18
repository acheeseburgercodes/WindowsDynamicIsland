using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DynamicIsland.Services;

public sealed class QuickControlService
{
    private static readonly Guid DeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    public double GetOutputVolume() => WithEndpoint(EDataFlow.Render, endpoint =>
        endpoint.GetMasterVolumeLevelScalar(out var value) == 0 ? value * 100 : 0, 0);

    public void SetOutputVolume(double percent) => WithEndpoint(EDataFlow.Render, endpoint =>
    {
        var context = Guid.Empty;
        endpoint.SetMasterVolumeLevelScalar((float)Math.Clamp(percent / 100, 0, 1), ref context);
        return true;
    }, false);

    public bool GetMicrophoneMuted() => WithEndpoint(EDataFlow.Capture, endpoint =>
        endpoint.GetMute(out var muted) == 0 && muted, false);

    public bool ToggleMicrophoneMute()
    {
        var muted = GetMicrophoneMuted();
        return WithEndpoint(EDataFlow.Capture, endpoint =>
        {
            var context = Guid.Empty;
            return endpoint.SetMute(!muted, ref context) == 0 ? !muted : muted;
        }, muted);
    }

    public static void OpenWindowsSurface(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });

    private static T WithEndpoint<T>(EDataFlow flow, Func<IAudioEndpointVolume, T> action, T fallback)
    {
        object? enumeratorObject = null;
        IMMDevice? device = null;
        object? volumeObject = null;
        try
        {
            var type = Type.GetTypeFromCLSID(DeviceEnumeratorClassId, throwOnError: true)!;
            enumeratorObject = Activator.CreateInstance(type)!;
            var enumerator = (IMMDeviceEnumerator)enumeratorObject;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(flow, ERole.Multimedia, out device));
            var id = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref id, CLSCTX.All, IntPtr.Zero, out volumeObject));
            return action((IAudioEndpointVolume)volumeObject);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return fallback; }
        finally { Release(volumeObject); Release(device); Release(enumeratorObject); }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }

    private enum EDataFlow { Render, Capture, All }
    private enum ERole { Console, Multimedia, Communications }
    [Flags] private enum CLSCTX : uint { InprocServer = 1, InprocHandler = 2, LocalServer = 4, RemoteServer = 16, All = 23 }

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

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
