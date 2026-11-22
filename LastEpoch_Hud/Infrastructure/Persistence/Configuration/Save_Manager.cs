using System.IO;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace LastEpoch_Hud.Scripts;

[RegisterTypeInIl2Cpp]
public partial class Save_Manager : MonoBehaviour
{
    public Save_Manager(System.IntPtr ptr)
        : base(ptr) { }

    public static Save_Manager instance { get; private set; }

    public string path = Directory.GetCurrentDirectory() + @"\Mods\" + Main.mod_name + @"\";
    string filename = "Save.json";
    public Data.Mods_Structure data = new Data.Mods_Structure();
    public Data.Mods_Structure data_duplicate = new Data.Mods_Structure();
    public bool initialized = false;
    bool menuWasOpen = false;
    bool loadFailed = false;

    void Awake()
    {
        instance = this;
    }

    async void Start()
    {
        await System.Threading.Tasks.Task.Run(() => Load());
    }

    void Update()
    {
        if (!initialized)
        {
            return;
        }
        // Equals walks the whole config with reflection. Doing that every frame hitches the game.
        // Settings only change from the mod menu, so compare while it is open and once as it closes.
        bool menuOpen = Hud_Manager.mod_menu_open;
        if (menuOpen || menuWasOpen)
        {
            Check_DataChanged();
        }
        menuWasOpen = menuOpen;
    }

    void Load()
    {
        try
        {
            var store = CreateStore();
            if (!store.TryLoad(out data))
            {
                Main.logger_instance?.Msg("Save Manager : Generate default config");
                data = Get_DefaultConfig();
                store.Save(data);
            }
        }
        catch (System.Exception ex)
        {
            loadFailed = true;
            Main.logger_instance?.Warning("Save Manager : Could not load config; existing file preserved: " + ex.Message);
            return;
        }
        Check_Update();
        if (!data.Items.Pickup.AutoShatter_Configured)
        {
            data.Items.Pickup.AutoShatter_Chance = 100;
            data.Items.Pickup.AutoShatter_AffixChance = 100;
            data.Items.Pickup.AutoShatter_QuantityChance = 0;
            data.Items.Pickup.AutoShatter_Configured = true;
            Save();
        }
        data_duplicate = data; //Use to check for data changed
        Main.logger_instance?.Msg("Save Manager : Data initialized");
        initialized = true;
    }

    void Check_Update()
    {
        bool data_changed = false;
        if (data.ModVersion != Main.mod_version)
        {
            //Update save when mod update here
            //data.KeyBinds.EternityCache_Past = KeyCode.F4;
            //data.KeyBinds.EternityCache_Future = KeyCode.F5;

            data.ModVersion = Main.mod_version;
            data_changed = true;
        }
        if (data_changed)
        {
            Save();
        }
    }

    void Check_DataChanged()
    {
        if (!data.Equals(data_duplicate))
        {
            Save();
            data_duplicate = data;
            if (!Mods_Manager.instance.IsNullOrDestroyed())
            {
                Mods_Manager.instance.change = true;
            } //Will be removed soon (when i have time to do it ^^)
        }
    }

    public void Save()
    {
        if (loadFailed) throw new IOException("Config could not be loaded; the existing file was preserved.");
        CreateStore().Save(data);
    }

    private LastEpoch_Hud.Scripts.Infrastructure.Persistence.AtomicJsonRepository<Data.Mods_Structure> CreateStore() =>
        new(path + filename, new LastEpoch_Hud.Scripts.Infrastructure.Persistence.JsonStorePolicy
        {
            Formatting = Formatting.Indented,
            KeepBackup = true,
        });
}
