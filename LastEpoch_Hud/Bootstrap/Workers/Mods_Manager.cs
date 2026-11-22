using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts;

[RegisterTypeInIl2Cpp]
public class Mods_Manager : MonoBehaviour
{
    public Mods_Manager(System.IntPtr ptr)
        : base(ptr) { }

    public static Mods_Manager instance { get; private set; }

    private readonly System.Collections.Generic.List<WorkerRegistration> workers = new();

    private sealed class WorkerRegistration
    {
        public string Name;
        public bool InitiallyActive = true;
        public bool Clone = true;
        public System.Action<GameObject> Attach;
        public System.Func<bool> Enabled;
        public GameObject Object;
    }

    // Concrete generic calls keep all IL2CPP component types visible at compile time.
    private static WorkerRegistration Worker<T>(string name, bool active = true,
        System.Func<bool> enabled = null, bool clone = true) where T : Component =>
        new() { Name = name, InitiallyActive = active, Clone = clone,
            Attach = obj => obj.AddComponent<T>(), Enabled = enabled };

    private void RegisterWorkers()
    {
        workers.Add(Worker<Mods.Character.Character_AutoPotions>("Mod_Character_AutoPotion", active: false, enabled: () => Save_Manager.instance.data.Character.Cheats.Enable_AutoPot));
        workers.Add(Worker<Mods.Character.Character_PotionReplenishment>("Mod_Character_PotionReplenishment"));
        workers.Add(Worker<Mods.Character.Character_Blessings>("Mod_Character_Blessings"));
        workers.Add(Worker<Mods.Character.Character_GodMode>("Mod_Character_GodMode", active: false, enabled: () => Save_Manager.instance.data.Character.Cheats.Enable_GodMode));
        workers.Add(Worker<Mods.Character.Character_LowLife>("Mod_Character_LowLife", active: false, enabled: () => Save_Manager.instance.data.Character.Cheats.Enable_LowLife));
        workers.Add(Worker<Mods.Character.Character_PermanentBuffs>("Mod_Character_Buffs"));
        workers.Add(Worker<Mods.Character.Character_TpSafe>("Mod_Character_SafeTp"));
        workers.Add(Worker<Mods.Character.Character_TwoHandedShield>("Mod_Character_TwoHandedShield"));
        workers.Add(Worker<Mods.Items.Drop.Items_SocketsNb>("Mod_Items_NbSocket"));
        workers.Add(Worker<Mods.Items.Pickup.Items_AutoStore_WithTimer>("Mod_Items_AutoStore_All10Sec", enabled: () => true));
        workers.Add(Worker<Mods.Items.CustomItems.Headhunter.Items_HeadHunter>("Mod_Items_Headhunter"));
        workers.Add(Worker<Mods.Items.CustomItems.Mjolner.Items_Mjolner>("Mod_Items_Mjolner"));
        workers.Add(Worker<Mods.Items.CustomItems.SandsOfSilk.Items_SandsOfSilk>("Mod_Items_SandsOfSilk"));
        workers.Add(Worker<Mods.Items.CustomItems.EssentiaSanguis.Items_EssentiaSanguis>("Mod_Items_EssentiaSanguis"));
        workers.Add(Worker<Mods.Items.CustomItems.Mageblood.Items_Mageblood>("Mod_Items_Mageblood"));
        workers.Add(Worker<Mods.Items.CustomItems.CustomItemIconUpdater>("Mod_Items_CustomItemIcons"));
        workers.Add(Worker<Mods.Items.Items_Temporalis>("Mod_Items_Temporalis"));
        workers.Add(Worker<Mods.Character.Character_Bank_Anywhere>("Mod_Bank_Anywhere"));
        workers.Add(Worker<Mods.Monoliths.Monoliths_CompleteObjective>("Mod_Monoliths_Complete_Objectives"));
        workers.Add(Worker<Mods.Bank.Bank_Quad>("mod_bank_quad"));
        workers.Add(Worker<Mods.UI.DamageMeter>("mod_damage_meter"));
        workers.Add(Worker<Mods.UI.SessionCounterRuntime>("Mod_SessionGainCounters", clone: false));
        var travel = Worker<Mods.Teleport.Teleport_ToScene>("Mod_Teleport_To_Scene");
        travel.Attach += obj => obj.AddComponent<Mods.Teleport.TravelAnywhere>();
        workers.Add(travel);
        workers.Add(Worker<Mods.Summon.Summon_Options>("Mod_Summon_Options", clone: false));
        workers.Add(Worker<Mods.Skills.Skills_AutoCast>("Mod_Skills_AutoCast", clone: false));
    }

    bool initialized = false;
    bool enable = false;
    public bool change = false;

    void Awake()
    {
        instance = this;
        Main.logger_instance?.Msg("Mods Manager : Initialize");
        RegisterWorkers();
        foreach (var worker in workers)
        {
            var template = new GameObject { name = worker.Name };
            var obj = worker.Clone
                ? Object.Instantiate(template, Vector3.zero, Quaternion.identity)
                : template;
            worker.Object = obj;
            obj.SetActive(worker.InitiallyActive);
            worker.Attach(obj);
        }
        foreach (var worker in workers) Object.DontDestroyOnLoad(worker.Object);
        initialized = true;
        Main.logger_instance?.Msg("Mods Manager : Mods initialized");
    }

    void Update() //This function will be removed soon (when i have time to do it ^^)
    {
        if (!Save_Manager.instance.IsNullOrDestroyed())
        {
            if (Save_Manager.instance.initialized)
            {
                if ((!enable) || (change))
                {
                    change = false;
                    Enable();
                }
            }
        }
    }

    void Enable() //This function will be removed soon (when i have time to do it ^^)
    {
        if (initialized)
        {
            enable = true;
            ApplyEnabled("Mod_Character_GodMode");
            ApplyEnabled("Mod_Character_LowLife");
            ApplyEnabled("Mod_Character_AutoPotion");
            ApplyEnabled("Mod_Items_AutoStore_All10Sec");
            Mods.Items.Requirements.Items_Update.Reqs(); //Used to update item req
        }
    }

    private void ApplyEnabled(string name)
    {
        foreach (var worker in workers)
            if (worker.Name == name && worker.Enabled != null)
            {
                worker.Object.SetActive(worker.Enabled());
                return;
            }
    }

}
