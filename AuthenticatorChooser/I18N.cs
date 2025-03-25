using AuthenticatorChooser.Resources;
using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.InteropServices;
using Workshell.PE;
using Workshell.PE.Resources;
using Workshell.PE.Resources.Strings;

namespace AuthenticatorChooser;

public static partial class I18N {

    public enum Key {

        SECURITY_KEY,
        SMARTPHONE,
        WINDOWS,
        SIGN_IN_WITH_YOUR_PASSKEY

    }

    private const uint MUI_LANGUAGE_NAME = 8;

    public static string userLocaleName { get; } = CultureInfo.CurrentCulture.Name;
    public static string userUiLocaleName { get; } = CultureInfo.CurrentUICulture.Name;
    public static string systemLocaleName { get; } = getCurrentSystemLocaleName();
    private static CultureInfo systemCulture { get; } = CultureInfo.GetCultureInfo(systemLocaleName);

    private static readonly FrozenDictionary<Key, IList<string>> STRINGS;
    private static readonly StringComparer                       STRING_COMPARER = StringComparer.CurrentCulture;

    static I18N() {
        StringTableResource.Register();

        string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";
        IList<(int stringTableId, int stringTableEntryId)> queries = [
            (15, 230), // Security key
            (15, 231), // Smartphone; also appears in webauthn.dll.mui string table 4 entries 50 and 56
            (15, 232)  // Windows
        ];

        // #2: CredentialUIBroker.exe runs as the current user
        IList<string?> fidoCredProvStringsUiLocale = getPeFileStrings(Path.Combine(systemRoot, "System32", userUiLocaleName, "fidocredprov.dll.mui"), queries);

        // User might have configured a second locale
        IList<string?> fidoCredProvStringsLocale = getPeFileStrings(Path.Combine(systemRoot, "System32", userLocaleName, "fidocredprov.dll.mui"), queries);

        // #2: CryptSvc runs as NETWORK SERVICE
        IList<string?> webauthnStrings = getPeFileStrings(Path.Combine(systemRoot, "System32", systemLocaleName, "webauthn.dll.mui"), [
            (4, 53) // Sign In With Your Passkey title; entry 63 has the same value, not sure which one is used
        ]);

        STRINGS = new Dictionary<Key, IList<string>> {
            [Key.SECURITY_KEY] = getUniqueNonNullStrings(Strings.securityKey, fidoCredProvStringsUiLocale[0], fidoCredProvStringsLocale[0]),
            [Key.SMARTPHONE]   = getUniqueNonNullStrings(Strings.smartphone, fidoCredProvStringsUiLocale[1], fidoCredProvStringsLocale[1]),
            [Key.WINDOWS]      = getUniqueNonNullStrings(Strings.windows, fidoCredProvStringsUiLocale[2], fidoCredProvStringsLocale[2]),
            [Key.SIGN_IN_WITH_YOUR_PASSKEY] = getUniqueNonNullStrings(Strings.ResourceManager.GetString(nameof(Strings.signInWithYourPasskey), systemCulture),
                webauthnStrings[0]),
        }.ToFrozenDictionary();

        static IList<string> getUniqueNonNullStrings(params string?[] strings) => strings.Compact().Distinct(STRING_COMPARER).ToList();
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

    private static unsafe string getCurrentSystemLocaleName() {
        int bufferSize = 0;
        getSystemPreferredUILanguages(MUI_LANGUAGE_NAME, out _, null, ref bufferSize);
        char[] buffer = new char[bufferSize];
        fixed (char* bufferStart = &buffer[0]) {
            getSystemPreferredUILanguages(MUI_LANGUAGE_NAME, out _, bufferStart, ref bufferSize);
        }
        var results = new ReadOnlySpan<char>(buffer, 0, bufferSize);
        return new string(results[..results.IndexOf('\0')]); // only return the first language name, even if buffer contains more than one (null-delimited)
    }

    /// <summary>
    /// <para><see href="https://learn.microsoft.com/en-us/windows/win32/intl/user-interface-language-management#system-ui-language"/></para>
    /// <para><see href="https://learn.microsoft.com/en-us/windows/win32/api/winnls/nf-winnls-getsystempreferreduilanguages"/></para>
    /// </summary>
    [LibraryImport("Kernel32.dll", EntryPoint = "GetSystemPreferredUILanguages", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool getSystemPreferredUILanguages(uint flags, out uint languageCount, char* resultBuffer, ref int resultBufferLength);

}