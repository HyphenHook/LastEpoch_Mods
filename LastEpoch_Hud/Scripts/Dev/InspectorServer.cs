using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Dev;

/// <summary>
/// Dev-only, read-only inspector. Off unless the environment variable LE_MOD_INSPECTOR is set, or the file
/// Mods\LastEpoch_Hud\inspector.enabled exists (value may be a port number, default 8765). Listens on loopback only. Requests are queued
/// and answered from the game's main thread, because IL2CPP objects must not be touched elsewhere.
/// </summary>
internal static class InspectorServer
{
    private const int DefaultPort = 8765;

    private sealed class Pending
    {
        public string Path;
        public NameValueCollection Query;
        public string Body;
        public int Status = 200;
        public readonly ManualResetEventSlim Done = new(false);
    }

    private static readonly System.Collections.Generic.Queue<Pending> _queue = new();
    private static HttpListener _listener;

    public static void Start(HarmonyLib.Harmony harmony)
    {
        string setting = Environment.GetEnvironmentVariable("LE_MOD_INSPECTOR");
        if (string.IsNullOrWhiteSpace(setting))
        {
            // Or drop a file named inspector.enabled into Mods\LastEpoch_Hud (optional port inside).
            string marker = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(),
                "Mods",
                Main.mod_name,
                "inspector.enabled"
            );
            if (System.IO.File.Exists(marker))
            {
                setting = System.IO.File.ReadAllText(marker).Trim();
                if (setting.Length == 0)
                {
                    setting = "1";
                }
            }
        }
        if (string.IsNullOrWhiteSpace(setting) || setting == "0")
        {
            return;
        }
        if (!int.TryParse(setting, out int port) || port < 1024 || port > 65535)
        {
            port = DefaultPort;
        }
        try
        {
            InspectorHooks.Install(harmony);
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _listener.Start();
            new Thread(Accept) { IsBackground = true, Name = "LE-Inspector" }.Start();
            Main.logger_instance?.Msg("[Inspector] listening on http://127.0.0.1:" + port + "/ (read-only, dev)");
            Main.logger_instance?.Msg("[Inspector] hooks:\n" + InspectorHooks.Report);
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("[Inspector] failed to start: " + ex.Message);
        }
    }

    public static void Stop()
    {
        try
        {
            _listener?.Stop();
        }
        catch (Exception) { }
    }

    /// <summary>Called from the main thread every frame; answers at most a few requests.</summary>
    public static void Tick()
    {
        GlyphDump.Tick();
        for (int i = 0; i < 4; i++)
        {
            Pending p;
            lock (_queue)
            {
                if (_queue.Count == 0)
                {
                    return;
                }
                p = _queue.Dequeue();
            }
            try
            {
                p.Body = Handle(p.Path, p.Query);
            }
            catch (Exception ex)
            {
                p.Status = 500;
                p.Body = ex.GetType().Name + ": " + (ex.InnerException ?? ex).Message;
            }
            p.Done.Set();
        }
    }

    private static void Accept()
    {
        while (_listener != null && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch (Exception)
            {
                return;
            }
            new Thread(() => Serve(ctx)) { IsBackground = true }.Start();
        }
    }

    private static void Serve(HttpListenerContext ctx)
    {
        var pending = new Pending();
        try
        {
            // Read-only, and refuse browser-originated requests (CSRF / DNS rebinding).
            string host = ctx.Request.Url.Host;
            if (
                ctx.Request.HttpMethod != "GET"
                || ctx.Request.Headers["Origin"] != null
                || (host != "127.0.0.1" && host != "localhost")
            )
            {
                pending.Status = 403;
                pending.Body = "forbidden";
            }
            else
            {
                pending.Path = ctx.Request.Url.AbsolutePath;
                pending.Query = System.Web.HttpUtility.ParseQueryString(ctx.Request.Url.Query);
                lock (_queue)
                {
                    _queue.Enqueue(pending);
                }
                if (!pending.Done.Wait(TimeSpan.FromSeconds(10)))
                {
                    pending.Status = 504;
                    pending.Body = "timed out waiting for the game's main thread (paused or loading?)";
                }
            }
            byte[] bytes = Encoding.UTF8.GetBytes(pending.Body ?? "");
            ctx.Response.StatusCode = pending.Status;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception) { }
        finally
        {
            try
            {
                ctx.Response.Close();
            }
            catch (Exception) { }
        }
    }

    private static string Handle(string path, NameValueCollection q)
    {
        switch (path)
        {
            case "/":
            case "/help":
                return "GET /ping | /scene | /scenes | /log?n=100\n"
                    + "  /tree?path=Root/Child&depth=2   (no path = all scene roots)\n"
                    + "  /find?type=Ability&name=REGEX   (live + asset objects of an interop type)\n"
                    + "  /types?pattern=REGEX&members=1  /search?pattern=REGEX (type + member names)\n"
                    + "  /dump?root=R&path=a.b.0&depth=1 where R is:\n"
                    + "      player | <captured type, see /instances> | static:Full.Type.Name | go:Root/Child/Obj (path starts with ComponentName)\n"
                    + "  /trace?n=100 | /counts | /instances | /hook?target=Full.Type.Name::Method (record-only)";
            case "/ping":
                return "pong";
            case "/scene":
                return "scene=" + Scenes.SceneName + " gameScene=" + Scenes.IsGameScene();
            case "/trace":
                return InspectorHooks.Trace(int.TryParse(q["n"], out int n) ? n : 100);
            case "/counts":
                return InspectorHooks.Counts();
            case "/instances":
                return string.Join("\n", InspectorHooks.InstanceNames);
            case "/types":
                return ObjectDumper.Types(q["pattern"] ?? ".", q["members"] == "1");
            case "/dump":
                return DumpRoot(q);
            case "/scenes":
                return InspectorScene.Scenes();
            case "/tree":
                return InspectorScene.Tree(q["path"], int.TryParse(q["depth"], out int td) ? Math.Clamp(td, 0, 6) : 1);
            case "/find":
                return InspectorScene.Find(q["type"], q["name"]);
            case "/search":
                return ObjectDumper.Search(q["pattern"] ?? ".");
            case "/log":
                return InspectorScene.LogTail(int.TryParse(q["n"], out int ln) ? Math.Clamp(ln, 1, 2000) : 100);
            case "/unhook":
                return InspectorHooks.RemoveTarget(q["target"] ?? "");
            case "/hook":
                return InspectorHooks.AddTarget(q["target"] ?? "");
            default:
                return "unknown endpoint; try /help";
        }
    }

    private static string DumpRoot(NameValueCollection q)
    {
        string root = q["root"] ?? "";
        int depth = int.TryParse(q["depth"], out int d) ? Math.Clamp(d, 0, 4) : 1;
        object target;
        if (root == "player")
        {
            target = Refs_Manager.player_actor;
        }
        else if (root.StartsWith("static:", StringComparison.Ordinal))
        {
            target = ObjectDumper.FindInteropType(root.Substring(7));
            if (target == null)
            {
                return "no type '" + root.Substring(7) + "'";
            }
        }
        else if (root.StartsWith("go:", StringComparison.Ordinal))
        {
            string full = root.Substring(3);
            string rest = q["path"] ?? "";
            int dot = rest.IndexOf('.');
            string comp = dot < 0 ? rest : rest.Substring(0, dot);
            if (comp.Length == 0)
            {
                return InspectorScene.Tree(full, 0) + "\n(add path=ComponentName[.member...] to dump a component)";
            }
            target = InspectorScene.Component(full, comp);
            if (target == null)
            {
                return "no component '" + comp + "' on " + full;
            }
            return ObjectDumper.Dump(target, dot < 0 ? "" : rest.Substring(dot + 1), depth);
        }
        else if (!InspectorHooks.TryGetInstance(root, out target))
        {
            return "no captured instance '" + root + "'. Captured: " + string.Join(", ", InspectorHooks.InstanceNames);
        }
        return ObjectDumper.Dump(target, q["path"], depth);
    }
}
