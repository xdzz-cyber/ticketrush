namespace TicketRush.BookingApi.Services;

/// <summary>
/// Lua-скрипти з Scripts/*.lua (embedded resources). StackExchange.Redis кешує SHA і
/// після першого виклику використовує EVALSHA, тож текст скрипта не передається щоразу.
/// </summary>
internal static class LuaScripts
{
    public static readonly string Reserve = Load("reserve.lua");
    public static readonly string Confirm = Load("confirm.lua");
    public static readonly string Reset = Load("reset.lua");

    private static string Load(string fileName)
    {
        var assembly = typeof(LuaScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
