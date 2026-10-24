namespace BlackEagle.Android;

internal static class TileRequest
{
    private static int pending;

    /// <summary>Запоминает запрос от защищенной системным разрешением плитки, а не из внешнего Intent.</summary>
    public static void Set() => Interlocked.Exchange(ref pending, 1);

    /// <summary>Однократно забирает запрос подключения после подготовки приложения.</summary>
    public static bool Consume() => Interlocked.Exchange(ref pending, 0) == 1;
}
