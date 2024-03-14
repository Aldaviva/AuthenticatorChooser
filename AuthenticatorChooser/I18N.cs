using AuthenticatorChooser.Resources;
using System.Globalization;
using Workshell.PE;
using Workshell.PE.Resources;
using Workshell.PE.Resources.Strings;

namespace AuthenticatorChooser;

public static class I18N {

    private const string FIDOCREDPROV_MUI_FILENAME = "fidocredprov.dll.mui";

    public enum Key {

        SECURITY_KEY,
        SMARTPHONE,
        WINDOWS

    }

    private static readonly IReadOnlyDictionary<Key, string?> RUNTIME_OS_FILE_STRINGS;

    static I18N() {
        StringTableResource.Register();

        string fidocredprovMuiFilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows", "System32", CultureInfo.CurrentUICulture.Name, FIDOCREDPROV_MUI_FILENAME);

        IList<string?> peFileStrings = getPeFileStrings(fidocredprovMuiFilePath, [
            (15, 230),
            (15, 231),
            (15, 232)
        ]);

        var strings = new Dictionary<Key, string?> {
            [Key.SECURITY_KEY] = peFileStrings[0],
            [Key.SMARTPHONE]   = peFileStrings[1],
            [Key.WINDOWS]      = peFileStrings[2]
        };
        RUNTIME_OS_FILE_STRINGS = strings.AsReadOnly();
    }

    public static string getStringCompileTime(Key key) => key switch {
        Key.SECURITY_KEY => Strings.securityKey,
        Key.SMARTPHONE   => Strings.smartphone,
        Key.WINDOWS      => Strings.windows,
        _                => throw new ArgumentOutOfRangeException(nameof(key), key, null)
    };

    public static string? getStringRuntime(Key key) => RUNTIME_OS_FILE_STRINGS[key];

    public static IEnumerable<string> getStrings(Key key) {
        yield return getStringCompileTime(key);

        if (getStringRuntime(key) is { } runtimeString) {
            yield return runtimeString;
        }
    }

    public static string? getPeFileString(string peFile, int stringTableId, int stringTableEntryId) {
        return getPeFileStrings(peFile, [(stringTableId, stringTableEntryId)])[0];
    }

    public static IList<string?> getPeFileStrings(string peFile, IList<(int stringTableId, int stringTableEntryId)> queries) {

        using PortableExecutableImage file = PortableExecutableImage.FromFile(peFile);

        IDictionary<int, StringTable?> stringTableCache = new Dictionary<int, StringTable?>();
        ResourceType?                  stringTables     = ResourceCollection.Get(file).FirstOrDefault(type => type.Id == ResourceType.String);
        IList<string?>                 results          = new List<string?>(queries.Count);

        foreach ((int stringTableId, int stringTableEntryId) in queries) {
            if (!stringTableCache.TryGetValue(stringTableId, out StringTable? stringTable)) {
                stringTable = (stringTables?.FirstOrDefault(resource => resource.Id == stringTableId) as StringTableResource)?.GetTable();

                stringTableCache[stringTableId] = stringTable;
            }

            results.Add(stringTable?.FirstOrDefault(entry => entry.Id == stringTableEntryId)?.Value);
        }

        return results;
    }

}