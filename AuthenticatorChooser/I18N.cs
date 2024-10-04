using AuthenticatorChooser.Resources;
using Microsoft.Win32;
using System.Collections.Frozen;
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

    private static readonly FrozenDictionary<Key, IList<string>> STRINGS;

    static I18N() {
        StringTableResource.Register();

        string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";

        // #2: CredentialUIBroker.exe runs as the current user
        IList<string?> fidoCredProvStrings = getPeFileStrings(Path.Combine(systemRoot, "System32", getUserLocaleId(true), "fidocredprov.dll.mui"), [
            (15, 230), // Security key
            (15, 231), // Smartphone; also appears in webauthn.dll.mui string table 4 entries 50 and 56
            (15, 232)  // Windows
        ]);

        // #2: CryptSvc runs as NETWORK SERVICE
        IList<string?> webauthnStrings = getPeFileStrings(Path.Combine(systemRoot, "System32", getUserLocaleId(false), "webauthn.dll.mui"), [
            (4, 53) // Sign In With Your Passkey title; entry 63 has the same value, not sure which one is used
        ]);

        STRINGS = new Dictionary<Key, IList<string>> {
            [Key.SECURITY_KEY]              = getUniqueNonNullStrings(Strings.securityKey, fidoCredProvStrings[0]),
            [Key.SMARTPHONE]                = getUniqueNonNullStrings(Strings.smartphone, fidoCredProvStrings[1]),
            [Key.WINDOWS]                   = getUniqueNonNullStrings(Strings.windows, fidoCredProvStrings[2]),
            [Key.SIGN_IN_WITH_YOUR_PASSKEY] = getUniqueNonNullStrings(Strings.signInWithYourPasskey, webauthnStrings[0]),
        }.ToFrozenDictionary();

        static IList<string> getUniqueNonNullStrings(params string?[] strings) => strings.Compact().Distinct(StringComparer.CurrentCulture).ToList();
    }

    public static IEnumerable<string> getStrings(Key key) => STRINGS[key];

    private static IList<string?> getPeFileStrings(string peFile, IList<(int stringTableId, int stringTableEntryId)> queries) {
        try {
            using PortableExecutableImage file = PortableExecutableImage.FromFile(peFile);

            IDictionary<int, StringTable?> stringTableCache = new Dictionary<int, StringTable?>(queries.Count);
            ResourceType?                  stringTables     = ResourceCollection.Get(file).FirstOrDefault(type => type.Id == ResourceType.String);
            IList<string?>                 results          = new List<string?>(queries.Count);

            foreach ((int stringTableId, int stringTableEntryId) in queries) {
                if (!stringTableCache.TryGetValue(stringTableId, out StringTable? stringTable)) {
                    StringTableResource? stringTableResource = stringTables?.FirstOrDefault(resource => resource.Id == stringTableId) as StringTableResource;
                    stringTable = stringTableResource?.GetTable(stringTableResource.Languages[0]); // #2: use the table's language, not always English

                    stringTableCache[stringTableId] = stringTable;
                }

                results.Add(stringTable?.FirstOrDefault(entry => entry.Id == stringTableEntryId)?.Value);
            }

            return results;
        } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { } catch (PortableExecutableImageException) { }

        return Enumerable.Repeat<string?>(null, queries.Count).ToList();
    }

    /// <summary>
    /// Get the current locale tag of the user or computer.
    /// </summary>
    /// <param name="currentUser"><c>true</c> to get the current user's locale, or <c>false</c> to get the locale of the system — specifically, the <c>NETWORK SERVICE</c> user</param>
    /// <returns>locale name, such as <c>en-US</c></returns>
    public static string getUserLocaleId(bool currentUser) => currentUser
        ? CultureInfo.CurrentUICulture.Name
        : (string) (Registry.GetValue(@"HKEY_USERS\S-1-5-20\Control Panel\International", "LocaleName", null) ?? string.Empty);

}