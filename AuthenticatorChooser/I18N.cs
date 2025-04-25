using AuthenticatorChooser.Resources;
using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.InteropServices;
using Unfucked;
using Workshell.PE;
using Workshell.PE.Resources;
using Workshell.PE.Resources.Strings;

namespace AuthenticatorChooser;

public static partial class I18N {

    public enum Key {

        // Windows
        SECURITY_KEY,
        SMARTPHONE,
        WINDOWS,
        SIGN_IN_WITH_YOUR_PASSKEY,

        // Chromium
        USE_A_SAVED_PASSKEY_FOR,
        WINDOWS_HELLO_OR_EXTERNAL_SECURITY_KEY

    }

    public static readonly  IReadOnlyList<string> LOCALE_NAMES = getCurrentSystemLocaleNames().Prepend(CultureInfo.CurrentUICulture.Name).Prepend(CultureInfo.CurrentCulture.Name).ToList();
    private static readonly FrozenDictionary<Key, IList<string>> STRINGS;
    private static readonly StringComparer STRING_COMPARER = StringComparer.CurrentCulture;
    private static readonly IDictionary<string, PortableExecutableImage?> DLL_CACHE = new Dictionary<string, PortableExecutableImage?>();
    private static readonly IDictionary<(string, int), StringTable?> STRING_TABLE_CACHE = new Dictionary<(string, int), StringTable?>();

    static I18N() {
        StringTableResource.Register();
        string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "C:\\Windows";

        STRINGS = new Dictionary<Key, IList<string>> {
            [Key.SECURITY_KEY] = getStrings(nameof(LocalizedStrings.securityKey), fidoCredProvMuiPath, 15, 230), // Security key
            [Key.SMARTPHONE] = getStrings(nameof(LocalizedStrings.smartphone), fidoCredProvMuiPath, 15, 231), // Smartphone; also appears in webauthn.dll.mui string table 4 entries 50 and 56
            [Key.WINDOWS] = getStrings(nameof(LocalizedStrings.windows), fidoCredProvMuiPath, 15, 232), // Windows
            [Key.SIGN_IN_WITH_YOUR_PASSKEY] = getStrings(nameof(LocalizedStrings.signInWithYourPasskey), webAuthnMuiPath, 4, 53), // Sign In With Your Passkey title; entry 63 has the same value
            [Key.USE_A_SAVED_PASSKEY_FOR] = getStrings(nameof(LocalizedStrings.useASavedPasskeyFor)).ToList(),
            [Key.WINDOWS_HELLO_OR_EXTERNAL_SECURITY_KEY] = getStrings(nameof(LocalizedStrings.windowsHelloOrExternalSecurityKey)).ToList()
        }.ToFrozenDictionary();

        foreach (PortableExecutableImage? dllFile in DLL_CACHE.Values) {
            dllFile?.Dispose();
        }

        STRING_TABLE_CACHE.Clear();
        DLL_CACHE.Clear();

        string fidoCredProvMuiPath(string locale) => Path.Combine(systemRoot, "System32", locale, "fidocredprov.dll.mui");
        string webAuthnMuiPath(string locale) => Path.Combine(systemRoot, "System32", locale, "webauthn.dll.mui");
    }

    public static IEnumerable<string> getStrings(Key key) => STRINGS[key];

    // #18: The most-preferred language pack can be missing MUI files if it was installed after Windows, so always fall back to all other preferred languages
    private static IList<string> getStrings(string compiledResourceName, Func<string, string> libraryPath, int stringTableId, int stringTableEntryId) =>
        getStrings(compiledResourceName)
            .Concat(LOCALE_NAMES.Select(locale => getPeFileString(libraryPath(locale), stringTableId, stringTableEntryId)))
            .Compact().Distinct(STRING_COMPARER).ToList();

    private static IEnumerable<string> getStrings(string compiledResourceName) => LOCALE_NAMES.Select(locale =>
            LocalizedStrings.ResourceManager.GetString(compiledResourceName, CultureInfo.GetCultureInfo(locale)))
        .Compact().Distinct(STRING_COMPARER);

    private static string? getPeFileString(string peFile, int stringTableId, int stringTableEntryId) {
        try {
            if (!STRING_TABLE_CACHE.TryGetValue((peFile, stringTableId), out StringTable? stringTable)) {
                if (!DLL_CACHE.TryGetValue(peFile, out PortableExecutableImage? file)) {
                    try {
                        file = PortableExecutableImage.FromFile(peFile);
                    } catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
                    DLL_CACHE.Add(peFile, file);
                }

                if (file != null) {
                    ResourceType?        stringTables        = ResourceCollection.Get(file).FirstOrDefault(type => type.Id == ResourceType.String);
                    StringTableResource? stringTableResource = stringTables?.FirstOrDefault(resource => resource.Id == stringTableId) as StringTableResource;
                    stringTable = stringTableResource?.GetTable(stringTableResource.Languages[0]); // #2: use the table's language, not always English
                }

                STRING_TABLE_CACHE.Add((peFile, stringTableId), stringTable);
            }

            return stringTable?.FirstOrDefault(entry => entry.Id == stringTableEntryId)?.Value;
        } catch (PortableExecutableImageException) {
            return null;
        }
    }

    private static unsafe string[] getCurrentSystemLocaleNames() {
        const uint MUI_LANGUAGE_NAME = 8;
        int        bufferSize        = 0;
        getSystemPreferredUILanguages(MUI_LANGUAGE_NAME, out _, null, ref bufferSize);
        char[] buffer = new char[bufferSize];
        uint   languageCount;
        fixed (char* bufferStart = &buffer[0]) {
            getSystemPreferredUILanguages(MUI_LANGUAGE_NAME, out languageCount, bufferStart, ref bufferSize);
        }
        var resultsBuffer = new ReadOnlySpan<char>(buffer, 0, bufferSize);
        // #18: Get all preferred languages, not just the first one, in case the most-preferred language pack is missing MUI files
        var resultsSplit = new Range[languageCount];
        resultsBuffer.Trim('\0').Split(resultsSplit, '\0'); // ReadOnlySpan.Split will leave delimiters intact if the destination span length is 1, which sucks, so trim early
        string[] results = new string[languageCount];
        for (int i = 0; i < languageCount; i++) {
            results[i] = resultsBuffer[resultsSplit[i]].ToString();
        }
        return results;
    }

    /// <summary>
    /// <para><see href="https://learn.microsoft.com/en-us/windows/win32/intl/user-interface-language-management#system-ui-language"/></para>
    /// <para><see href="https://learn.microsoft.com/en-us/windows/win32/api/winnls/nf-winnls-getsystempreferreduilanguages"/></para>
    /// </summary>
    [LibraryImport("Kernel32.dll", EntryPoint = "GetSystemPreferredUILanguages", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool getSystemPreferredUILanguages(uint flags, out uint languageCount, char* resultBuffer, ref int resultBufferLength);

}