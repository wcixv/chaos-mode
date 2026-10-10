// ModLoader: tiny Doorstop entrypoint that loads every .dll in the game's "Mods" folder.
// Each mod exposes:  public static class ModMain { public static void OnGameReady() }
// which is called once, on the main thread, when the first scene loads.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Doorstop
{
    public static class Entrypoint
    {
        public static void Start()
        {
            try
            {
                ModLoader.Loader.Log("Doorstop entry reached.");
                var t = new System.Threading.Thread(ModLoader.Loader.WaitForGame);
                t.IsBackground = true;
                t.Start();
            }
            catch (Exception e) { ModLoader.Loader.Log("Start failed: " + e); }
        }
    }
}

namespace ModLoader
{
    public static class Loader
    {
        private static string root;
        private static bool started;
        private static readonly List<Assembly> mods = new List<Assembly>();

        private static string Root
        {
            get
            {
                if (root == null) root = Path.GetDirectoryName(Path.GetDirectoryName(typeof(Loader).Assembly.Location));
                return root;
            }
        }

        public static void Log(string msg)
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(typeof(Loader).Assembly.Location), "ModLoader.log");
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        // The game's runtime is stripped (no AssemblyLoad event), so poll until the game code is loaded.
        public static void WaitForGame()
        {
            try
            {
                bool ready = false;
                for (int i = 0; i < 1200 && !ready; i++)
                {
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                        if (a.GetName().Name == "Assembly-CSharp") { ready = true; break; }
                    if (!ready) System.Threading.Thread.Sleep(100);
                }
                if (!ready) { Log("Game code never loaded; giving up."); return; }

                string dir = Path.Combine(Root, "Mods");
                if (!Directory.Exists(dir)) { Log("No Mods folder at " + dir); return; }
                foreach (var file in Directory.GetFiles(dir, "*.dll"))
                {
                    try { mods.Add(Assembly.LoadFrom(file)); Log("Loaded " + Path.GetFileName(file)); }
                    catch (Exception e) { Log("Failed to load " + Path.GetFileName(file) + ": " + e.Message); }
                }
                HookScenes();
            }
            catch (Exception e) { Log("Loader failed: " + e); }
        }

        // Separate method so Unity types are only touched after Unity's assemblies exist.
        private static void HookScenes()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (started) return;
            started = true;
            foreach (var asm in mods)
            {
                try
                {
                    Type t = null;
                    foreach (var type in asm.GetTypes()) if (type.Name == "ModMain") { t = type; break; }
                    var m = t?.GetMethod("OnGameReady", BindingFlags.Public | BindingFlags.Static);
                    if (m == null) { Log(asm.GetName().Name + ": no ModMain.OnGameReady, skipped."); continue; }
                    m.Invoke(null, null);
                    Log(asm.GetName().Name + " started in scene '" + scene.name + "'.");
                }
                catch (Exception e) { Log(asm.GetName().Name + " failed to start: " + e); }
            }
        }
    }
}
