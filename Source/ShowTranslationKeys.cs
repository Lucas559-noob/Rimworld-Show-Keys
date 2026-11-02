using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ShowTranslationKeys
{
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            Harmony harmony = new Harmony("YourName.ShowTranslationKeys");
            int patched = 0;

            foreach (MethodInfo m in typeof(Translator).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "Translate") continue;
                if (m.ReturnType == typeof(string))
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.TranslateString_Postfix)));
                    patched++;
                }
                else if (m.ReturnType == typeof(TaggedString))
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.TranslateTagged_Postfix)));
                    patched++;
                }
            }

            string[] labelProps = new string[] { "label", "LabelCap" };
            foreach (string propName in labelProps)
            {
                PropertyInfo prop = typeof(Def).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) continue;
                MethodInfo getter = prop.GetGetMethod();
                if (getter == null) continue;
                if (prop.PropertyType == typeof(string))
                {
                    harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.DefLabelString_Postfix)));
                    patched++;
                }
                else if (prop.PropertyType == typeof(TaggedString))
                {
                    harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.DefLabelTagged_Postfix)));
                    patched++;
                }
            }

            List<string> overrides = new List<string>();
            try
            {
                foreach (Type type in typeof(Def).Assembly.GetTypes())
                {
                    if (type == null) continue;
                    if (type == typeof(Def)) continue;
                    if (!typeof(Def).IsAssignableFrom(type)) continue;

                    foreach (string propName in labelProps)
                    {
                        PropertyInfo prop;
                        try
                        {
                            prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        }
                        catch
                        {
                            continue;
                        }
                        if (prop == null) continue;
                        MethodInfo getter = prop.GetGetMethod();
                        if (getter == null) continue;

                        try
                        {
                            if (prop.PropertyType == typeof(TaggedString))
                            {
                                harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.DefLabelTagged_Postfix)));
                                patched++;
                                overrides.Add(type.Name + "." + propName);
                            }
                            else if (prop.PropertyType == typeof(string))
                            {
                                harmony.Patch(getter, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.DefLabelString_Postfix)));
                                patched++;
                                overrides.Add(type.Name + "." + propName);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Failed to scan Def subclasses: " + e.Message);
            }

            if (overrides.Count > 0)
            {
                Log.Message("[ShowTranslationKeys] Patched overrides: " + string.Join(", ", overrides));
            }

            MethodInfo fdf = AccessTools.Method(typeof(BackstoryDef), "FullDescriptionFor");
            if (fdf != null)
            {
                if (fdf.ReturnType == typeof(TaggedString))
                {
                    harmony.Patch(fdf, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.BackstoryDesc_Tagged_Postfix)));
                    patched++;
                }
                else if (fdf.ReturnType == typeof(string))
                {
                    harmony.Patch(fdf, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.BackstoryDesc_String_Postfix)));
                    patched++;
                }
            }

            MethodInfo rootUpdate = AccessTools.Method(typeof(Root), "Update");
            if (rootUpdate != null)
            {
                harmony.Patch(rootUpdate, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.RootUpdate_Postfix)));
                patched++;
            }

            Log.Message("[ShowTranslationKeys] Loaded. " + patched + " methods patched.");

            if (!Prefs.DevMode)
            {
                Log.Warning("[ShowTranslationKeys] Dev mode is OFF. Key wrapping is disabled.");
            }

            RunEncodingCheck();
        }

        // Silent unless something is wrong. If the player's translation files are
        // saved with the wrong encoding, this is the first place it shows up.
        static void RunEncodingCheck()
        {
            try
            {
                string sample = "STK_SettingsCategory".Translate();
                if (string.IsNullOrEmpty(sample) || sample == "STK_SettingsCategory")
                {
                    Log.Warning("[ShowTranslationKeys] Settings strings not loaded. "
                        + "Check that Languages/English/Keyed/ShowTranslationKeys.xml exists and is saved as UTF-8.");
                    return;
                }
                if (sample.IndexOf('\u00C3') >= 0 || sample.IndexOf('\u00C2') >= 0)
                {
                    Log.Error("[ShowTranslationKeys] Translation files appear to be saved in a non-UTF-8 encoding. "
                        + "Re-save them as UTF-8 (Notepad++: Encoding > Convert to UTF-8; VS Code: Save with Encoding > UTF-8).");
                }
            }
            catch
            {
            }
        }
    }

    internal static class Requirements
    {
        public static bool CanWrapKeys
        {
            get { return Prefs.DevMode; }
        }

        public static bool CanSpawn
        {
            get
            {
                ShowTranslationKeysSettings s = ShowTranslationKeysMod.Settings;
                if (s == null) return Prefs.DevMode;
                if (!s.spawnRequiresDevMode) return true;
                return Prefs.DevMode;
            }
        }
    }

    internal static class KeyWhitelist
    {
        public static bool IsWhitelisted(string key)
        {
            if (string.IsNullOrEmpty(key)) return true;
            if (key.StartsWith("STK_", StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public class ShowTranslationKeysSettings : ModSettings
    {
        public string targetKey = "";
        public bool logCaller = true;
        public bool highlightTarget = true;
        public bool wrapKeys = true;
        public string detectedUiClass = "";
        public string manualUiClass = "";
        public int hotkey = (int)KeyCode.F10;
        public string contextDefName = "";
        public int wrapper = 0;
        public string keyPrefix = "";
        public bool spawnRequiresDevMode = true;
        public List<string> detectedHistory = new List<string>();
        public List<string> knownUis = new List<string>();
        public string uiFilter = "";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref targetKey, "targetKey", "");
            Scribe_Values.Look(ref logCaller, "logCaller", true);
            Scribe_Values.Look(ref highlightTarget, "highlightTarget", true);
            Scribe_Values.Look(ref wrapKeys, "wrapKeys", true);
            Scribe_Values.Look(ref detectedUiClass, "detectedUiClass", "");
            Scribe_Values.Look(ref manualUiClass, "manualUiClass", "");
            Scribe_Values.Look(ref hotkey, "hotkey", (int)KeyCode.F10);
            Scribe_Values.Look(ref contextDefName, "contextDefName", "");
            Scribe_Values.Look(ref wrapper, "wrapper", 0);
            Scribe_Values.Look(ref keyPrefix, "keyPrefix", "");
            Scribe_Values.Look(ref spawnRequiresDevMode, "spawnRequiresDevMode", true);
            Scribe_Values.Look(ref uiFilter, "uiFilter", "");
            Scribe_Collections.Look(ref detectedHistory, "detectedHistory", LookMode.Value);
            Scribe_Collections.Look(ref knownUis, "knownUis", LookMode.Value);
            if (detectedHistory == null) detectedHistory = new List<string>();
            if (knownUis == null) knownUis = new List<string>();
        }

        public void AddDetected(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return;
            if (detectedHistory == null) detectedHistory = new List<string>();
            if (!detectedHistory.Contains(cls)) detectedHistory.Add(cls);
        }

        public string GetEffectiveUiClass()
        {
            if (!string.IsNullOrEmpty(manualUiClass) && manualUiClass.Contains("."))
            {
                if (UiOpener.ResolveType(manualUiClass) != null) return manualUiClass;
            }
            return detectedUiClass;
        }

        public string GetEffectiveDefName()
        {
            if (!string.IsNullOrEmpty(contextDefName)) return contextDefName;
            if (string.IsNullOrEmpty(targetKey)) return null;

            string name = targetKey;
            string[] suffixes = new string[] { ".label", ".LabelCap", ".description", ".baseDesc", ".title" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (name.EndsWith(suffixes[i]))
                {
                    name = name.Substring(0, name.Length - suffixes[i].Length);
                    break;
                }
            }
            return name;
        }
    }

    public class ShowTranslationKeysMod : Mod
    {
        public static ShowTranslationKeysSettings Settings;
        static Vector2 outerScroll = Vector2.zero;

        public ShowTranslationKeysMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ShowTranslationKeysSettings>();
        }

        public override string SettingsCategory()
        {
            return "STK_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, 1800f);
            Widgets.BeginScrollView(inRect, ref outerScroll, viewRect);
            try
            {
                Listing_Standard list = new Listing_Standard();
                list.Begin(viewRect);
                DrawSettings(list);
                list.End();
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        void DrawSettings(Listing_Standard list)
        {
            if (!Prefs.DevMode)
            {
                Color oldColor = GUI.color;
                GUI.color = Color.yellow;
                string warn = "STK_DevModeWarning".Translate();
                list.Label(warn);
                GUI.color = oldColor;
                list.Gap(4f);
            }

            string wrapLabel = "STK_WrapKeysLabel".Translate();
            list.CheckboxLabeled(wrapLabel, ref Settings.wrapKeys);

            string spawnLabel = "STK_SpawnRequiresDevMode".Translate();
            list.CheckboxLabeled(spawnLabel, ref Settings.spawnRequiresDevMode);
            list.Gap(8f);

            string keySection = "STK_TargetKeySection".Translate();
            list.Label(keySection);

            string keyPrompt = "STK_TargetKeyLabel".Translate();
            list.Label(keyPrompt);
            string currentKey = Settings.targetKey;
            if (currentKey == null) currentKey = "";
            string newKey = list.TextEntry(currentKey);
            Settings.targetKey = newKey;

            string logLabel = "STK_LogCaller".Translate();
            list.CheckboxLabeled(logLabel, ref Settings.logCaller);

            string hlLabel = "STK_HighlightTarget".Translate();
            list.CheckboxLabeled(hlLabel, ref Settings.highlightTarget);
            list.Gap(10f);

            string ctxSection = "STK_ContextSection".Translate();
            list.Label(ctxSection);

            string ctxPrompt = "STK_ContextDefLabel".Translate();
            list.Label(ctxPrompt);
            string currentDef = Settings.contextDefName;
            if (currentDef == null) currentDef = "";
            string newDef = list.TextEntry(currentDef);
            Settings.contextDefName = newDef;

            string ctxHint = "STK_ContextDefHint".Translate();
            list.Label(ctxHint);

            string effectiveDef = Settings.GetEffectiveDefName();
            Def foundDef = ContextGenerator.FindDef(effectiveDef);
            if (foundDef != null)
            {
                string defInfo = "STK_DefDetected".Translate(foundDef.GetType().Name, foundDef.defName);
                list.Label(defInfo);

                string genLabel = "STK_GenerateContextButton".Translate(foundDef.defName);
                if (list.ButtonText(genLabel))
                {
                    ContextGenerator.Generate(foundDef);
                }
            }
            else if (effectiveDef != null)
            {
                string noDef = "STK_NoDefFound".Translate(effectiveDef);
                list.Label(noDef);
            }
            else
            {
                string hint = "STK_TypeDefNameHint".Translate();
                list.Label(hint);
            }

            string rerollLabel = "STK_RerollTaleButton".Translate();
            if (list.ButtonText(rerollLabel))
            {
                ContextGenerator.RerollTale();
            }
            list.Gap(10f);

            string detSection = "STK_DetectedUISection".Translate();
            list.Label(detSection);

            string detectedUi = Settings.detectedUiClass;
            if (string.IsNullOrEmpty(detectedUi))
            {
                string noUi = "STK_NoUIDetected".Translate();
                list.Label(noUi);
            }
            else
            {
                list.Label(detectedUi);
            }

            string manualPrompt = "STK_ManualUILabel".Translate();
            list.Label(manualPrompt);
            string currentManual = Settings.manualUiClass;
            if (currentManual == null) currentManual = "";
            string newManual = list.TextEntry(currentManual);
            Settings.manualUiClass = newManual;

            KeyCode currentKeyCode = (KeyCode)Settings.hotkey;
            string hotkeyText = currentKeyCode.ToString();
            string hotkeyLabel = "STK_HotkeyLabel".Translate(hotkeyText);
            list.Label(hotkeyLabel);

            string newHotkey = list.TextEntry(hotkeyText);
            if (newHotkey != hotkeyText)
            {
                KeyCode parsed = currentKeyCode;
                bool ok = Enum.TryParse<KeyCode>(newHotkey, true, out parsed);
                if (ok)
                {
                    Settings.hotkey = (int)parsed;
                }
            }

            string hotkeyHint = "STK_HotkeyHint".Translate();
            list.Label(hotkeyHint);

            string openLabel = "STK_OpenUIButton".Translate();
            if (list.ButtonText(openLabel))
            {
                UiOpener.TryOpen(Settings.GetEffectiveUiClass());
            }
            list.Gap(10f);

            string wrapperName;
            if (Settings.wrapper == 0)
            {
                wrapperName = "STK_WrapperGuillemets".Translate();
            }
            else
            {
                wrapperName = "STK_WrapperBrackets".Translate();
            }

            string wrapperButton = "STK_WrapperLabel".Translate(wrapperName);
            if (list.ButtonText(wrapperButton))
            {
                Settings.wrapper = (Settings.wrapper + 1) % 2;
            }

            string prefixPrompt = "STK_KeyPrefixLabel".Translate();
            list.Label(prefixPrompt);
            string currentPrefix = Settings.keyPrefix;
            if (currentPrefix == null) currentPrefix = "";
            string newPrefix = list.TextEntry(currentPrefix);
            Settings.keyPrefix = newPrefix;

            string prefixHint = "STK_KeyPrefixHint".Translate();
            list.Label(prefixHint);
            list.Gap(10f);

            string catSection = "STK_UiCatalogSection".Translate();
            list.Label(catSection);

            string scanLabel = "STK_ScanUIsButton".Translate();
            if (list.ButtonText(scanLabel))
            {
                Settings.knownUis = UiCatalog.ScanAll();
                Log.Message("[ShowTranslationKeys] Found " + Settings.knownUis.Count + " UIs.");
            }

            int uiCount = 0;
            if (Settings.knownUis != null) uiCount = Settings.knownUis.Count;
            string uiCountLabel = "STK_UIsInList".Translate(uiCount);
            list.Label(uiCountLabel);

            string filterLabel = "STK_FilterLabel".Translate();
            list.Label(filterLabel);
            string currentFilter = Settings.uiFilter;
            if (currentFilter == null) currentFilter = "";
            string newFilter = list.TextEntry(currentFilter);
            Settings.uiFilter = newFilter;

            bool showMatches = false;
            if (!string.IsNullOrEmpty(Settings.uiFilter) && Settings.knownUis != null && Settings.knownUis.Count > 0)
            {
                showMatches = true;
            }

            if (showMatches)
            {
                List<string> matches = Settings.knownUis
                    .Where(u => u.IndexOf(Settings.uiFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(8).ToList();

                if (matches.Count == 0)
                {
                    string noResults = "STK_NoResults".Translate();
                    list.Label(noResults);
                }
                else
                {
                    foreach (string m in matches)
                    {
                        string buttonText = "-> " + m;
                        if (list.ButtonText(buttonText))
                        {
                            Settings.manualUiClass = m;
                            Settings.uiFilter = "";
                        }
                    }
                }
            }
            list.Gap(8f);

            bool hasHistory = false;
            if (Settings.detectedHistory != null && Settings.detectedHistory.Count > 0)
            {
                hasHistory = true;
            }

            if (hasHistory)
            {
                string histSection = "STK_DetectedHistorySection".Translate();
                list.Label(histSection);

                List<string> history = Settings.detectedHistory.Take(6).ToList();
                foreach (string h in history)
                {
                    string btn = "-> " + h;
                    if (list.ButtonText(btn))
                    {
                        Settings.manualUiClass = h;
                    }
                }
                list.Gap(8f);
            }

            string hintText = "STK_LogHint".Translate();
            list.Label(hintText);
        }
    }

        internal static class KeyFormatter
    {
        public static string Wrap(string key)
        {
            int w = 0;
            if (ShowTranslationKeysMod.Settings != null)
            {
                w = ShowTranslationKeysMod.Settings.wrapper;
            }
            if (w == 1)
            {
                return "[[" + key + "]]";
            }
            return "\u00AB" + key + "\u00BB";
        }

        public static string Highlight(string key)
        {
            return "[\u25BA" + key + "\u25C4]";
        }
    }

    internal static class TargetLogger
    {
        static readonly HashSet<string> Logged = new HashSet<string>();

        public static bool IsTarget(string key)
        {
            ShowTranslationKeysSettings s = ShowTranslationKeysMod.Settings;
            if (s == null) return false;
            if (string.IsNullOrEmpty(s.targetKey)) return false;
            if (key == s.targetKey) return true;
            if (s.targetKey.EndsWith(".label") && key == s.targetKey.Substring(0, s.targetKey.Length - 6)) return true;
            if (s.targetKey.EndsWith(".LabelCap") && key == s.targetKey.Substring(0, s.targetKey.Length - 9)) return true;
            return false;
        }

        public static void OnTargetKey(string key)
        {
            ShowTranslationKeysSettings s = ShowTranslationKeysMod.Settings;
            if (s == null) return;

            string sig;
            string uiClass;
            try
            {
                sig = BuildSignature(out uiClass);
            }
            catch
            {
                return;
            }

            if (!Logged.Add(sig)) return;
            Log.Message("[ShowTranslationKeys] Target key '" + key + "' used by:\n" + sig);

            if (!string.IsNullOrEmpty(uiClass))
            {
                s.AddDetected(uiClass);
                if (uiClass != s.detectedUiClass)
                {
                    s.detectedUiClass = uiClass;
                    Log.Message("[ShowTranslationKeys] Detected UI: '" + uiClass + "'.");
                }
            }
        }

        static string BuildSignature(out string uiClass)
        {
            uiClass = null;
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(2, false);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < st.FrameCount && i < 12; i++)
            {
                System.Diagnostics.StackFrame frame = st.GetFrame(i);
                if (frame == null) continue;
                MethodBase m = frame.GetMethod();
                if (m == null) continue;
                Type dt = m.DeclaringType;
                string typeName = "?";
                if (dt != null) typeName = dt.FullName;
                sb.Append("  <- ").Append(typeName).Append('.').Append(m.Name).Append('\n');

                if (uiClass == null && dt != null && dt.Namespace != null && dt.Namespace.StartsWith("RimWorld"))
                {
                    if (typeof(Window).IsAssignableFrom(dt) || typeof(MainTabWindow).IsAssignableFrom(dt))
                    {
                        uiClass = dt.FullName;
                    }
                }
            }
            return sb.ToString();
        }
    }

    internal static class UiOpener
    {
        public static Type ResolveType(string className)
        {
            if (string.IsNullOrEmpty(className)) return null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(className, false);
                    if (t != null) return t;
                }
                catch
                {
                }
            }
            return null;
        }

        public static void TryOpen(string className)
        {
            if (!Requirements.CanSpawn)
            {
                Log.Warning("[ShowTranslationKeys] Dev mode required to open UIs.");
                return;
            }

            if (string.IsNullOrEmpty(className))
            {
                Log.Warning("[ShowTranslationKeys] No UI detected yet.");
                return;
            }

            Type t = ResolveType(className);
            if (t == null)
            {
                Log.Warning("[ShowTranslationKeys] Type '" + className + "' not found in loaded assemblies.");
                return;
            }

            if (typeof(MainTabWindow).IsAssignableFrom(t))
            {
                foreach (MainButtonDef def in DefDatabase<MainButtonDef>.AllDefs)
                {
                    if (def.tabWindowClass == t)
                    {
                        Find.MainTabsRoot.SetCurrentTab(def);
                        Log.Message("[ShowTranslationKeys] Tab opened: " + className);
                        return;
                    }
                }
                Log.Warning("[ShowTranslationKeys] MainTabWindow '" + className + "' has no matching MainButtonDef.");
                return;
            }

            if (typeof(Window).IsAssignableFrom(t))
            {
                try
                {
                    ConstructorInfo ctor = t.GetConstructor(Type.EmptyTypes);
                    if (ctor == null)
                    {
                        Log.Warning("[ShowTranslationKeys] '" + className + "' has no parameterless constructor.");
                        return;
                    }
                    Window w = (Window)ctor.Invoke(null);
                    Find.WindowStack.Add(w);
                    Log.Message("[ShowTranslationKeys] Opened: " + className);
                }
                catch (Exception e)
                {
                    string msg = e.Message;
                    if (e.InnerException != null) msg = e.InnerException.Message;
                    Log.Warning("[ShowTranslationKeys] Failed to open '" + className + "': " + msg);
                }
                return;
            }

            Log.Warning("[ShowTranslationKeys] '" + className + "' is not a Window/MainTabWindow.");
        }
    }

    internal static class UiCatalog
    {
        public static List<string> ScanAll()
        {
            List<string> result = new List<string>();
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type t in types)
                {
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (t.IsInterface) continue;
                    if (!typeof(Window).IsAssignableFrom(t)) continue;
                    try
                    {
                        if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    }
                    catch
                    {
                        continue;
                    }
                    try
                    {
                        result.Add(t.FullName);
                    }
                    catch
                    {
                    }
                }
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
    }

    internal static class Patches
    {
        static bool TryFormat(string key, out string formatted)
        {
            formatted = null;

            if (string.IsNullOrEmpty(key)) return false;

            ShowTranslationKeysSettings s = ShowTranslationKeysMod.Settings;
            if (s == null) return false;
            if (!Requirements.CanWrapKeys) return false;
            if (KeyWhitelist.IsWhitelisted(key)) return false;

            bool isTarget = TargetLogger.IsTarget(key);
            if (isTarget) TargetLogger.OnTargetKey(key);

            if (!s.wrapKeys) return false;

            bool prefixMatch = string.IsNullOrEmpty(s.keyPrefix) || key.StartsWith(s.keyPrefix, StringComparison.Ordinal);
            if (!prefixMatch && !isTarget) return false;

            if (isTarget && s.highlightTarget)
            {
                formatted = KeyFormatter.Highlight(key);
            }
            else
            {
                formatted = KeyFormatter.Wrap(key);
            }
            return true;
        }

        public static void TranslateString_Postfix(string key, ref string __result)
        {
            string formatted;
            bool replace = TryFormat(key, out formatted);
            if (replace)
            {
                __result = formatted;
            }
        }

        public static void TranslateTagged_Postfix(string key, ref TaggedString __result)
        {
            string formatted;
            bool replace = TryFormat(key, out formatted);
            if (replace)
            {
                __result = new TaggedString(formatted);
            }
        }

        public static void DefLabelString_Postfix(Def __instance, ref string __result)
        {
            if (__instance == null) return;
            string formatted;
            bool replace = TryFormat(__instance.defName, out formatted);
            if (replace)
            {
                __result = formatted;
            }
        }

        public static void DefLabelTagged_Postfix(Def __instance, ref TaggedString __result)
        {
            if (__instance == null) return;
            string formatted;
            bool replace = TryFormat(__instance.defName, out formatted);
            if (replace)
            {
                __result = new TaggedString(formatted);
            }
        }

        public static void BackstoryDesc_Tagged_Postfix(BackstoryDef __instance, ref TaggedString __result)
        {
            if (__instance == null) return;
            string formatted;
            bool replace = TryFormat(__instance.defName + ".baseDesc", out formatted);
            if (replace)
            {
                __result = new TaggedString(formatted);
            }
        }

        public static void BackstoryDesc_String_Postfix(BackstoryDef __instance, ref string __result)
        {
            if (__instance == null) return;
            string formatted;
            bool replace = TryFormat(__instance.defName + ".baseDesc", out formatted);
            if (replace)
            {
                __result = formatted;
            }
        }

        public static void RootUpdate_Postfix()
        {
            if (!Requirements.CanSpawn) return;
            ShowTranslationKeysSettings s = ShowTranslationKeysMod.Settings;
            if (s == null) return;
            string uiClass = s.GetEffectiveUiClass();
            if (string.IsNullOrEmpty(uiClass)) return;
            if (!Input.GetKeyDown((KeyCode)s.hotkey)) return;
            UiOpener.TryOpen(uiClass);
        }
    }

    internal static class ContextGenerator
    {
        static readonly Type[] DbTypes = new Type[]
        {
            typeof(DefDatabase<BackstoryDef>),
            typeof(DefDatabase<TraitDef>),
            typeof(DefDatabase<HediffDef>),
            typeof(DefDatabase<PawnKindDef>),
            typeof(DefDatabase<ThingDef>),
            typeof(DefDatabase<GeneDef>),
            typeof(DefDatabase<PreceptDef>),
            typeof(DefDatabase<ResearchProjectDef>),
            typeof(DefDatabase<TaleDef>),
        };

        public static Def FindDef(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            foreach (Type dbType in DbTypes)
            {
                try
                {
                    MethodInfo getMethod = dbType.GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static);
                    if (getMethod == null) continue;
                    object[] args = new object[] { defName };
                    Def def = getMethod.Invoke(null, args) as Def;
                    if (def != null) return def;
                }
                catch
                {
                }
            }
            return null;
        }

        public static void Generate(Def def)
        {
            if (!Requirements.CanSpawn)
            {
                Log.Warning("[ShowTranslationKeys] Dev mode required to spawn context.");
                return;
            }

            BackstoryDef backstory = def as BackstoryDef;
            if (backstory != null)
            {
                GenerateBackstory(backstory);
                return;
            }

            TraitDef trait = def as TraitDef;
            if (trait != null)
            {
                GenerateTrait(trait);
                return;
            }

            HediffDef hediff = def as HediffDef;
            if (hediff != null)
            {
                GenerateHediff(hediff);
                return;
            }

            PawnKindDef kind = def as PawnKindDef;
            if (kind != null)
            {
                GeneratePawnKind(kind);
                return;
            }

            ThingDef thing = def as ThingDef;
            if (thing != null)
            {
                GenerateThing(thing);
                return;
            }

            GeneDef gene = def as GeneDef;
            if (gene != null)
            {
                GenerateGene(gene);
                return;
            }

            PreceptDef precept = def as PreceptDef;
            if (precept != null)
            {
                GeneratePrecept(precept);
                return;
            }

            ResearchProjectDef research = def as ResearchProjectDef;
            if (research != null)
            {
                OpenResearch(research);
                return;
            }

            TaleDef tale = def as TaleDef;
            if (tale != null)
            {
                GenerateTale(tale);
                return;
            }

            Log.Warning("[ShowTranslationKeys] Unsupported Def type: " + def.GetType().Name);
        }

        static Pawn FindColonist()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return null;
            try
            {
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (p == null) continue;
                    if (p.Dead) continue;
                    return p;
                }
            }
            catch
            {
            }
            return null;
        }

        public static void RerollTale()
        {
            if (!Requirements.CanSpawn)
            {
                Log.Warning("[ShowTranslationKeys] Dev mode required to reroll tales.");
                return;
            }

            Thing selected = Find.Selector.SingleSelectedThing;
            if (selected == null)
            {
                Log.Warning("[ShowTranslationKeys] Select an art thing first.");
                return;
            }

            CompArt compArt = selected.TryGetComp<CompArt>();
            if (compArt == null)
            {
                Log.Warning("[ShowTranslationKeys] Selected thing is not art (no CompArt).");
                return;
            }

            Pawn creator = FindColonist();
            if (creator == null)
            {
                Log.Warning("[ShowTranslationKeys] Need at least one living colonist to record the tale.");
                return;
            }

            try
            {
                compArt.JustCreatedBy(creator);
                Log.Message("[ShowTranslationKeys] Tale rerolled on '" + selected.def.defName + "'.");
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Reroll failed: " + e.Message);
            }
        }

        static void GenerateTale(TaleDef taleDef)
        {
            Log.Message("[ShowTranslationKeys] TaleDef '" + taleDef.defName + "' is chosen randomly when art is created. Use 'Reroll tale' until it appears.");

            // In 1.6 there is no bare "Sculpture" ThingDef; it is split into three sizes.
            string[] candidates = new string[] { "SculptureGrand", "SculptureLarge", "SculptureSmall" };
            ThingDef sculpture = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                sculpture = DefDatabase<ThingDef>.GetNamedSilentFail(candidates[i]);
                if (sculpture != null) break;
            }
            if (sculpture == null)
            {
                Log.Warning("[ShowTranslationKeys] Could not find any sculpture ThingDef.");
                return;
            }
            GenerateThing(sculpture);
        }

        static void GenerateBackstory(BackstoryDef backstory)
        {
            if (backstory.slot != BackstorySlot.Childhood && backstory.slot != BackstorySlot.Adulthood)
            {
                Log.Warning("[ShowTranslationKeys] Backstory '" + backstory.defName + "' has unsupported slot '" + backstory.slot + "'.");
                return;
            }

            Pawn pawn = GenerateColonist();
            if (pawn == null) return;

            try
            {
                if (backstory.slot == BackstorySlot.Childhood)
                {
                    pawn.story.Childhood = backstory;
                }
                else
                {
                    pawn.story.Adulthood = backstory;
                }
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Failed to assign backstory: " + e.Message);
            }

            SelectAndInspect(pawn);
            Log.Message("[ShowTranslationKeys] Pawn spawned with backstory '" + backstory.defName + "'.");
        }

        static void GenerateTrait(TraitDef traitDef)
        {
            Pawn pawn = GenerateColonist();
            if (pawn == null) return;
            try
            {
                pawn.story.traits.GainTrait(new Trait(traitDef));
                SelectAndInspect(pawn);
                Log.Message("[ShowTranslationKeys] Trait '" + traitDef.defName + "' applied.");
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Trait failed: " + e.Message);
            }
        }

        static void GenerateHediff(HediffDef hediffDef)
        {
            Pawn pawn = GenerateColonist();
            if (pawn == null) return;
            try
            {
                pawn.health.AddHediff(hediffDef);
                SelectAndInspect(pawn);
                Log.Message("[ShowTranslationKeys] Hediff '" + hediffDef.defName + "' applied.");
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Hediff failed: " + e.Message);
            }
        }

        static void GenerateGene(GeneDef geneDef)
        {
            if (!ModsConfig.BiotechActive)
            {
                Log.Warning("[ShowTranslationKeys] Biotech is not active.");
                return;
            }
            Pawn pawn = GenerateColonist();
            if (pawn == null) return;
            try
            {
                // Xenogene overrides endogenous genes, which is usually what a
                // translator wants when testing a single gene key.
                pawn.genes.AddGene(geneDef, true);
                SelectAndInspect(pawn);
                Log.Message("[ShowTranslationKeys] Gene '" + geneDef.defName + "' added as xenogene.");
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Gene failed: " + e.Message);
            }
        }

        static void GeneratePawnKind(PawnKindDef kindDef)
        {
            if (Find.AnyPlayerHomeMap == null)
            {
                Log.Warning("[ShowTranslationKeys] No active colony.");
                return;
            }
            try
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kindDef, Faction.OfPlayer);
                SelectAndInspect(pawn);
                Log.Message("[ShowTranslationKeys] PawnKind '" + kindDef.defName + "' spawned.");
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] PawnKind failed: " + e.Message);
            }
        }

        static void GenerateThing(ThingDef thingDef)
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null)
            {
                Log.Warning("[ShowTranslationKeys] No active colony.");
                return;
            }

            try
            {
                Thing thing = ThingMaker.MakeThing(thingDef);
                CompArt compArt = thing.TryGetComp<CompArt>();

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                Find.Selector.Select(thing, true);

                if (compArt != null)
                {
                    Pawn creator = FindColonist();
                    if (creator != null)
                    {
                        try
                        {
                            compArt.JustCreatedBy(creator);
                            Log.Message("[ShowTranslationKeys] Art '" + thingDef.defName + "' spawned with a random tale. Use 'Reroll tale' to try another.");
                        }
                        catch (Exception e)
                        {
                            Log.Warning("[ShowTranslationKeys] Art tale generation failed: " + e.Message);
                        }
                    }
                    else
                    {
                        Log.Warning("[ShowTranslationKeys] Art '" + thingDef.defName + "' spawned, but no colonist is available to record a tale.");
                    }
                }
                else
                {
                    Log.Message("[ShowTranslationKeys] Thing '" + thingDef.defName + "' spawned.");
                }
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Thing failed: " + e.Message);
            }
        }

        static void GeneratePrecept(PreceptDef preceptDef)
        {
            if (!ModsConfig.IdeologyActive)
            {
                Log.Warning("[ShowTranslationKeys] Ideology is not active.");
                return;
            }
            try
            {
                Ideo ideo = null;
                if (Faction.OfPlayer.ideos != null)
                {
                    ideo = Faction.OfPlayer.ideos.PrimaryIdeo;
                }
                if (ideo == null)
                {
                    Log.Warning("[ShowTranslationKeys] Player faction has no primary ideoligion.");
                    return;
                }

                Type makerType = AccessTools.TypeByName("RimWorld.PreceptMaker");
                if (makerType == null)
                {
                    Log.Warning("[ShowTranslationKeys] PreceptMaker not found.");
                    return;
                }
                MethodInfo make = makerType.GetMethod("MakePrecept", BindingFlags.Public | BindingFlags.Static);
                if (make == null)
                {
                    Log.Warning("[ShowTranslationKeys] PreceptMaker.MakePrecept not found.");
                    return;
                }

                // Signature changed across versions. Try common forms in order.
                Precept precept = null;
                object[][] argSets = new object[][]
                {
                    new object[] { preceptDef, ideo },
                    new object[] { preceptDef },
                };
                for (int i = 0; i < argSets.Length; i++)
                {
                    if (precept != null) break;
                    try
                    {
                        precept = make.Invoke(null, argSets[i]) as Precept;
                    }
                    catch (TargetParameterCountException)
                    {
                        continue;
                    }
                }
                if (precept == null)
                {
                    Log.Warning("[ShowTranslationKeys] Failed to create precept (signature mismatch).");
                    return;
                }

                MethodInfo add = typeof(Ideo).GetMethod("AddPrecept", BindingFlags.Public | BindingFlags.Instance);
                if (add == null)
                {
                    Log.Warning("[ShowTranslationKeys] Ideo.AddPrecept not found.");
                    return;
                }
                object[] addArgs = new object[] { precept, true };
                add.Invoke(ideo, addArgs);

                Log.Message("[ShowTranslationKeys] Precept '" + preceptDef.defName + "' added to ideoligion.");
                MainButtonDef tab = DefDatabase<MainButtonDef>.GetNamedSilentFail("Ideos");
                if (tab != null) Find.MainTabsRoot.SetCurrentTab(tab);
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Precept failed: " + e.Message);
            }
        }

        static void OpenResearch(ResearchProjectDef projectDef)
        {
            try
            {
                MainButtonDef tab = DefDatabase<MainButtonDef>.GetNamedSilentFail("Research");
                if (tab != null)
                {
                    Find.MainTabsRoot.SetCurrentTab(tab);
                    Log.Message("[ShowTranslationKeys] Research tab opened. Look for '" + projectDef.defName + "'.");
                }
                else
                {
                    Log.Warning("[ShowTranslationKeys] Research tab not found.");
                }
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Research failed: " + e.Message);
            }
        }

        static Pawn GenerateColonist()
        {
            if (Find.AnyPlayerHomeMap == null)
            {
                Log.Warning("[ShowTranslationKeys] No active colony.");
                return null;
            }
            try
            {
                PawnGenerationRequest req = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist,
                    Faction.OfPlayer,
                    PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false);
                return PawnGenerator.GeneratePawn(req);
            }
            catch (Exception e)
            {
                Log.Warning("[ShowTranslationKeys] Colonist generation failed: " + e.Message);
                return null;
            }
        }

        static void SelectAndInspect(Pawn pawn)
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
            GenSpawn.Spawn(pawn, cell, map);
            Find.Selector.Select(pawn, true);
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect);
        }
    }
}
