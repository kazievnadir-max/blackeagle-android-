using System.Runtime.InteropServices;

namespace BlackEagle.Android;

internal static class AwgNative
{
    /// <summary>Защищает транспортный UDP-сокет от попадания внутрь собственного VPN.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ProtectSocket(int fd);

    /// <summary>Генерирует приватный и публичный ключи X25519 внутри нативной обертки.</summary>
    [DllImport("blackeagle_awg", EntryPoint = "be_awg_keys", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Keys([Out] byte[] privateKey, [Out] byte[] publicKey);

    /// <summary>Передает дескриптор TUN и проверенные параметры неизмененному AWG-движку.</summary>
    [DllImport("blackeagle_awg", EntryPoint = "be_awg_start", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Start(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string configuration, ProtectSocket protect);

    /// <summary>Читает время последнего подтвержденного handshake без экспорта секретных параметров.</summary>
    [DllImport("blackeagle_awg", EntryPoint = "be_awg_handshake", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long Handshake();

    /// <summary>Закрывает устройство AWG вместе с принадлежащими ему дескрипторами.</summary>
    [DllImport("blackeagle_awg", EntryPoint = "be_awg_stop", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Stop();
}
