using AuthenticatorChooser.Resources;
using System.Globalization;
using Workshell.PE;
using Workshell.PE.Resources;
using Workshell.PE.Resources.Strings;

namespace AuthenticatorChooser;

public static class I18N {

    public enum Key {

        SECURITY_KEY,
        SMARTPHONE,
        WINDOWS,
        SIGN_IN_WITH_YOUR_PASSKEY

    }

    private static readonly IReadOnlyDictionary<Key, string?> RUNTIME_OS_FILE_STRINGS;

    static I18N() {
        StringTableResource.Register();

        string localizedFilesDir = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows", "System32", CultureInfo.CurrentUICulture.Name);

        IList<string?> fidoCredProvStrings = getPeFileStrings(Path.Combine(localizedFilesDir, "fidocredprov.dll.mui"), [
            (15, 230),
            (15, 231), // also appears in webauthn.dll.mui string table 4 entries 50 and 56
            (15, 232)
        ]);

        IList<string?> webauthnStrings = getPeFileStrings(Path.Combine(localizedFilesDir, "webauthn.dll.mui"), [
            (4, 53) // entry 63 has the same value, not sure which one is used
        ]);

        RUNTIME_OS_FILE_STRINGS = new Dictionary<Key, string?> {
            [Key.SECURITY_KEY]              = fidoCredProvStrings[0],
            [Key.SMARTPHONE]                = fidoCredProvStrings[1],
            [Key.WINDOWS]                   = fidoCredProvStrings[2],
            [Key.SIGN_IN_WITH_YOUR_PASSKEY] = webauthnStrings[0]
        }.AsReadOnly();
    }

    private static string getStringCompileTime(Key key) => key switch {
        Key.SECURITY_KEY              => Strings.securityKey,
        Key.SMARTPHONE                => Strings.smartphone,
        Key.WINDOWS                   => Strings.windows,
        Key.SIGN_IN_WITH_YOUR_PASSKEY => Strings.signInWithYourPasskey,
        _                             => throw new ArgumentOutOfRangeException(nameof(key), key, null)
    };

    private static string? getStringRuntime(Key key) => RUNTIME_OS_FILE_STRINGS.GetValueOrDefault(key);

    public static IEnumerable<string> getStrings(Key key) {
        yield return getStringCompileTime(key);

        if (getStringRuntime(key) is { } runtimeString) {
            yield return runtimeString;
        }
    }

    private static IList<string?> getPeFileStrings(string peFile, IList<(int stringTableId, int stringTableEntryId)> queries) {
        try {
            using PortableExecutableImage file = PortableExecutableImage.FromFile(peFile);

            IDictionary<int, StringTable?> stringTableCache = new Dictionary<int, StringTable?>();
            ResourceType?                  stringTables     = ResourceCollection.Get(file).FirstOrDefault(type => type.Id == ResourceType.String);
            IList<string?>                 results          = new List<string?>(queries.Count);

            foreach ((int stringTableId, int stringTableEntryId) in queries) {
                if (!stringTableCache.TryGetValue(stringTableId, out StringTable? stringTable)) {
                    StringTableResource? stringTableResource = stringTables?.FirstOrDefault(resource => resource.Id == stringTableId) as StringTableResource;
                    stringTable = stringTableResource?.GetTable(stringTableResource.Languages[0]);

                    stringTableCache[stringTableId] = stringTable;
                }

                results.Add(stringTable?.FirstOrDefault(entry => entry.Id == stringTableEntryId)?.Value);
            }

            return results;
        } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }

        return [];
    }

}