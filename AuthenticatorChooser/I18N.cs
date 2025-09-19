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

        /// <summary>
        /// Security key
        /// </summary>
        SECURITY_KEY,

        /// <summary>
        /// iPhone, iPad, or Android device
        /// </summary>
        SMARTPHONE,

        /// <summary>
        /// This Windows device
        /// </summary>
        WINDOWS,

        /// <summary>
        /// Sign in with your passkey
        /// </summary>
        SIGN_IN_WITH_YOUR_PASSKEY,

        /// <summary>
        /// Use another device
        /// </summary>
        USE_ANOTHER_DEVICE,

        /// <summary>
        /// Making sure it’s you
        /// </summary>
        MAKING_SURE_ITS_YOU,

        /// <summary>
        /// Choose a passkey
        /// </summary>
        CHOOSE_A_PASSKEY,

        /// <summary>
        /// Sign in with a passkey
        /// </summary>
        SIGN_IN_WITH_A_PASSKEY,

        /// <summary>
        /// Choose a different passkey
        /// </summary>
        CHOOSE_A_DIFFERENT_PASSKEY

    }

    public static readonly IReadOnlyList<string> LOCALE_NAMES = getCurrentSystemLocaleNames().Prepend(CultureInfo.CurrentUICulture.Name).Prepend(CultureInfo.CurrentCulture.Name)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static readonly StringComparer                                STRING_COMPARER = StringComparer.CurrentCulture;
    private static readonly FrozenDictionary<Key, IList<string>>          STRINGS;
    private static readonly IDictionary<string, PortableExecutableImage?> DLL_CACHE          = new Dictionary<string, PortableExecutableImage?>();
    private static readonly IDictionary<(string, int), StringTable?>      STRING_TABLE_CACHE = new Dictionary<(string, int), StringTable?>();

    static I18N() {
        StringTableResource.Register();
        string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";

        STRINGS = new Dictionary<Key, IList<string>> {
            [Key.SECURITY_KEY] = getStrings(nameof(LocalizedStrings.securityKey), fidoCredProvMuiPath, 15, 230), // Security key
            [Key.SMARTPHONE] = getStrings(nameof(LocalizedStrings.smartphone), fidoCredProvMuiPath, 15, 231), // Smartphone; also appears in webauthn.dll.mui string table 4 entries 50 and 56
            [Key.WINDOWS] = getStrings(nameof(LocalizedStrings.windows), fidoCredProvMuiPath, 15, 232), // Windows
            [Key.SIGN_IN_WITH_YOUR_PASSKEY] = getStrings(nameof(LocalizedStrings.signInWithYourPasskey), webAuthnMuiPath, 4, 53), // Sign In With Your Passkey title; entry 63 has the same value
            [Key.USE_ANOTHER_DEVICE] = getStrings(nameof(LocalizedStrings.useAnotherDevice), fidoCredProvMuiPath, 15, 234), // Use another device
            [Key.MAKING_SURE_ITS_YOU] = getStrings(nameof(LocalizedStrings.makingSureItsYou), ngcCredProvMuiPath, 35, 554), // Making sure it’s you
            [Key.CHOOSE_A_PASSKEY] = getStrings(nameof(LocalizedStrings.chooseAPasskey), webAuthnMuiPath, 67, 1057), // Choose a passkey
            [Key.SIGN_IN_WITH_A_PASSKEY] = getStrings(nameof(LocalizedStrings.signInWithAPasskey), webAuthnMuiPath, 65, 1037), // Sign in with a passkey
            [Key.CHOOSE_A_DIFFERENT_PASSKEY] = getStrings(nameof(LocalizedStrings.chooseADifferentPasskey), webAuthnMuiPath, 65, 1029), // Sign in with a passkey
        }.ToFrozenDictionary();

        foreach (PortableExecutableImage? dllFile in DLL_CACHE.Values) {
            dllFile?.Dispose();
        }

        STRING_TABLE_CACHE.Clear();
        DLL_CACHE.Clear();

        string fidoCredProvMuiPath(string locale) => Path.Combine(systemRoot, "System32", locale, "fidocredprov.dll.mui");
        string webAuthnMuiPath(string locale) => Path.Combine(systemRoot, "System32", locale, "webauthn.dll.mui");
        string ngcCredProvMuiPath(string locale) => Path.Combine(systemRoot, "System32", locale, "ngccredprov.dll.mui");
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