using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using LeveDeliver.Data;
using LeveDeliver.Game;
using LeveDeliver.IPC;
using LeveDeliver.UI;

namespace LeveDeliver;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Leve Deliver";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Configuration configuration;
    private readonly LeveDatabase database;
    private readonly PandoraIPC pandora;
    private readonly AddonFlow flow;
    private readonly WindowSystem windowSystem;
    private readonly MainWindow mainWindow;
    private readonly DeliverAllOverlay overlay;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IChatGui chat,
        IGameGui gameGui,
        IDataManager dataManager,
        IObjectTable objectTable,
        ITargetManager targetManager,
        ICondition condition,
        IClientState clientState,
        IPluginLog log,
        ICommandManager commandManager)
    {
        this.pluginInterface = pluginInterface;
        Service.PluginInterface = pluginInterface;
        Service.Framework = framework;
        Service.Chat = chat;
        Service.GameGui = gameGui;
        Service.Data = dataManager;
        Service.Objects = objectTable;
        Service.Targets = targetManager;
        Service.Condition = condition;
        Service.ClientState = clientState;
        Service.Log = log;
        Service.Commands = commandManager;
        ECommons.ECommonsMain.Init(pluginInterface, this, ECommons.Module.DalamudReflector);

        this.configuration = Configuration.Load(pluginInterface);

        // Load the embedded leve database (data attributed to xivganon/LEDE, MIT).
        this.database = LeveDatabase.Load(pluginInterface);

        this.pandora = new PandoraIPC(pluginInterface, Service.Chat);
        try
        {
            this.pandora.CheckInstalled();
            this.pandora.ChatStatus();
        }
        catch (Exception ex)
        {
            // Pandora's Box detection is a soft dependency: if reflection fails for
            // any reason, degrade to the built-in slot filler instead of failing load.
            this.pandora.SetUnavailable();
            Service.Log.Warning(ex, "Pandora's Box detection failed — using built-in slot filler.");
        }

        this.flow = new AddonFlow(this.database, this.pandora, this.configuration, pluginInterface.UiBuilder);

        this.windowSystem = new WindowSystem("LeveDeliver");
        this.mainWindow = new MainWindow(this.configuration, this.flow, this.pandora);
        this.windowSystem.AddWindow(this.mainWindow);

        this.overlay = new DeliverAllOverlay(this.flow, this.configuration);

        pluginInterface.UiBuilder.Draw += this.Draw;
        pluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
        pluginInterface.UiBuilder.Draw += this.overlay.Draw;

        Service.Framework.Update += this.FrameworkUpdate;

        Service.Commands.AddHandler("/levedeliver", new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Toggle the Leve Deliver window. Use '/levedeliver start' to start delivery with a leve selected.",
            ShowInHelp = true,
        });
    }

    public void Dispose()
    {
        Service.Commands.RemoveHandler("/levedeliver");
        Service.Framework.Update -= this.FrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= this.overlay.Draw;
        pluginInterface.UiBuilder.Draw -= this.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
        this.windowSystem.RemoveAllWindows();
        this.flow.Dispose();
        if (this.pandora.Installed)
            this.pandora.Dispose();
        this.mainWindow.Dispose();
        this.overlay.Dispose();
        this.configuration.Dispose();
        ECommons.ECommonsMain.Dispose();
    }

    private void FrameworkUpdate(IFramework framework)
    {
        this.flow.Tick();
    }

    private void Draw()
    {
        this.windowSystem.Draw();
    }

    private void OpenMainUi()
    {
        this.mainWindow.IsOpen = true;
    }

    private void OpenConfigUi()
    {
        this.mainWindow.IsOpen = true;
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            if (!this.flow.StartFromGuildLeve())
                Service.Chat.PrintError("LeveDeliver: could not start — is a leve selected in the leve window?");
            return;
        }

        this.mainWindow.Toggle();
    }
}
