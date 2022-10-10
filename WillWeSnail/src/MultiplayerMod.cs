
using GmmlHooker;
using GmmlInteropGenerator;
using GmmlInteropGenerator.Types;
using GmmlPatcher;

using Lidgren.Network;

using UndertaleModLib;
using UndertaleModLib.Models;

namespace WillWeSnail;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class MultiplayerMod : IGameMakerMod
{
    public static readonly Guid id = Guid.NewGuid();

    private static readonly NetPeerConfiguration config = new("Will We Snail?");
    private static readonly NetClient client = new(config);
    private static readonly Dictionary<Guid, PlayerData> players = new();

    private enum MessageType : short { WtfWhereZero, PlayerConnected }

    public void Load(int audioGroup, UndertaleData data, ModData currentMod)
    {
        if (audioGroup != 0)
            return;

        
        data.CreateGlobalScript("mp_global_init", @"
global.mpActive = false
instance_create_layer(0,0,layer_create(0),obj_mp_manager)
global.mpDataPosX = ds_map_create()
global.mpDataPosY = ds_map_create()
global.mpDataVelX = ds_map_create()
global.mpDataVelY = ds_map_create()
global.mpDataLookdir = ds_map_create()
global.mpDataRoom = ds_map_create()", 0, out _);

        SetupMultiplayerManager(data);
        SetupMultiplayerPlayer(data);

        data.HookCode("gml_Object_obj_player_Step_2", @"
if(global.mpActive){
    mp_sendMovement(lookdir,room)
}
#orig#()");

        data.CreateLegacyScript("mp_updatePlayers", @"
playerCount = mp_getPlayerCount()
for(i = 0; i < playerCount; i++){
    guid = mp_getPlayerGuid(i)
    ds_map_set(global.mpDataPosX, guid, mp_getPlayerPosX(guid))
    ds_map_set(global.mpDataPosY, guid, mp_getPlayerPosY(guid))
    ds_map_set(global.mpDataVelX, guid, mp_getPlayerVelX(guid))
    ds_map_set(global.mpDataVelY, guid, mp_getPlayerVelY(guid))
    ds_map_set(global.mpDataLookdir, guid, mp_getPlayerLookdir(guid))
    ds_map_set(global.mpDataRoom, guid, mp_getPlayerRoom(guid))
}
mpsnails = ds_list_create()
for(i = 0; i < instance_number(obj_mp_player); i++){
    ds_list_set(mpsnails, i, instance_find(obj_mp_player,i).guid)
}
players = []
players = ds_map_keys_to_array(global.mpDataRoom)
for(i = 0; i < array_length(players); i++){
    if(ds_list_find_index(mpsnails,players[i]) == -1){
        if(ds_map_find_value(global.mpDataRoom, players[i]) != room){
            return 0
        }
        newSnail = instance_create_layer(0,0,""Player"",obj_mp_player)
        newSnail.guid = players[i]
    }else{
        if(ds_map_find_value(global.mpDataRoom, players[i]) != room){
            instance_destroy(ds_list_find_index(mpsnails,players[i]))
        }
    }
}
return 0", 0);
    }
        
        
        
    private static void SetupMultiplayerManager(UndertaleData data)
    {
        UndertaleGameObject multiplayerManager = new()
        {
            Name = data.Strings.MakeString("obj_mp_manager"),
            Persistent = true
        };
        data.GameObjects.Add(multiplayerManager);

        multiplayerManager.EventHandlerFor(EventType.Create, data.Strings, data.Code, data.CodeLocals)
            .ReplaceGmlSafe("show_debug_message(\"Multiplayer Object Created\")", data);

        multiplayerManager.EventHandlerFor(EventType.Step, EventSubtypeStep.Step, data)
            .ReplaceGmlSafe(@"
if(!global.mpActive){
    return 0
}
mp_manage()
mp_updatePlayers()", data);

        multiplayerManager.EventHandlerFor(EventType.KeyRelease, EventSubtypeKey.vk_f5, data)
            .ReplaceGmlSafe(@"
if(global.mpActive==false){
    global.mpActive=true
    mp_connect(get_string(""IP"",""127.0.0.1""),get_integer(""Port"",42069))
}else{
    global.mpActive=false
    mp_disconnect()
}", data);
    }

    private static void SetupMultiplayerPlayer(UndertaleData data)
    {
        UndertaleGameObject multiplayerPlayer = new() {
            Name = data.Strings.MakeString("obj_mp_player")
        };
        multiplayerPlayer.EventHandlerFor(EventType.Create, data.Strings, data.Code, data.CodeLocals)
            .AppendGmlSafe(@"
        guid=""0""
        house_height=1
        house_width=1
        house_tilt=0
        lookdir=0
        if (instance_exists(obj_levelstyler))
        {
	        if (variable_instance_exists(obj_levelstyler.id, ""col_snail_body""))
                col_snail_body = obj_levelstyler.col_snail_body;
                col_snail_outline = obj_levelstyler.col_snail_outline;
                col_snail_shell = obj_levelstyler.col_snail_shell;
                col_snail_eye = obj_levelstyler.col_snail_eye;
        }
        ", data);
        multiplayerPlayer.EventHandlerFor(EventType.Step, EventSubtypeStep.Step, data)
            .AppendGmlSafe(@"
            x = ds_map_find_value(global.mpDataPosX, guid)
            y = ds_map_find_value(global.mpDataPosY, guid)
            hspeed = ds_map_find_value(global.mpDataVelX, guid)
            vspeed = ds_map_find_value(global.mpDataVelY, guid)
            lookdir = ds_map_find_value(global.mpDataLookdir, guid)
            myroom = ds_map_find_value(global.mpDataRoom, guid)
            if(myroom != room){
                instance_destroy()
            }
            ", data);
        multiplayerPlayer.EventHandlerFor(EventType.Draw, EventSubtypeDraw.Draw, data)
            .AppendGmlSafe(@"
            house_height = lerp(house_height, 1+(vspeed*.05),2)
            house_height=clamp(house_height,0.4,1.6)
            house_width=clamp(1/house_height,.8,5)
            house_tilt=lerp(house_tilt,hspeed,0.1)
            house_sprite=69
            draw_sprite_ext(house_sprite, 0, x - (-15 * lookdir), y + 16, house_width * -1*lookdir, house_height, house_tilt, col_snail_shell, 1);
            draw_sprite_ext(house_sprite, 1, x - (-15 * lookdir), y + 16, house_width * -1*lookdir, house_height, house_tilt, col_snail_outline, 1);
            draw_sprite_ext(spr_player_base, 0, x, y, image_xscale * -1*lookdir, image_yscale, image_angle, col_snail_body, 1);
            draw_sprite_ext(spr_player_base, 1, x, y, image_xscale * -1*lookdir, image_yscale, image_angle, col_snail_outline, 1);
            ", data);
        data.GameObjects.Add(multiplayerPlayer);
        try
        {
            data.Code.First(code => code.Name.Content == "gml_Object_obj_epilepsy_warning_Create_0").AppendGmlSafe("global.mpActive=false\ninstance_create_layer(0,0,layer_create(0),obj_mp_manager)\nglobal.mpDataPosX = ds_map_create()\nglobal.mpDataPosY = ds_map_create()\nglobal.mpDataVelX = ds_map_create()\nglobal.mpDataVelY = ds_map_create()\nglobal.mpDataLookdir = ds_map_create()\nglobal.mpDataRoom = ds_map_create()", data);
        }
        catch { }
    }



    [GmlInterop("mp_disconnect")]
    public static void multiplayerDisconnect(ref CInstance self, ref CInstance other)
    {
        client.Disconnect("Leaving");
        Console.WriteLine("Disconnected from multiplayer");
    }

    [GmlInterop("mp_getPlayerCount")]
    public static double MpGetPlayerCount(ref CInstance self, ref CInstance other) => players.Count;

    [GmlInterop("mp_getPlayerGuid")]
    public static string MpGetPlayerGuid(ref CInstance self, ref CInstance other, int position) =>
        players.Keys.ToList()[position].ToString();

    [GmlInterop("mp_getPlayerPosX")]
    public static double MpGetPlayerPosX(ref CInstance self, ref CInstance other, string playerId) =>
        players[new Guid(playerId)].posX;

    [GmlInterop("mp_getPlayerPosY")]
    public static double MpGetPlayerPosY(ref CInstance self, ref CInstance other, string playerId) =>
        players[new Guid(playerId)].posY;

    [GmlInterop("mp_getPlayerVelX")]
    public static double MpGetPlayerVelX(ref CInstance self, ref CInstance other, string playerId) =>
        players[new Guid(playerId)].hSpeed;

    [GmlInterop("mp_getPlayerVelY")]
    public static double MpGetPlayerVelY(ref CInstance self, ref CInstance other, string playerId) =>
        players[new Guid(playerId)].vSpeed;

    [GmlInterop("mp_getPlayerLookdir")]
    public static int MpGetPlayerLookdir(ref CInstance self, ref CInstance other, string playerId) =>
        players[new Guid(playerId)].lookDir ? 1 : -1;

    [GmlInterop("mp_getPlayerRoom")]
    public static double MpGetPlayerRoom(ref CInstance self, ref CInstance other, string playerId) =>
        players[new Guid(playerId)].room;

    [GmlInterop("mp_connect")]
    public static void MultiplayerConnect(ref CInstance self, ref CInstance other, string ip, int port)
    {
        client.Start();
        client.Connect(ip, port);
        Console.WriteLine("Started connection");
    }

    [GmlInterop("mp_manage")]
    public static void MpManage(ref CInstance self, ref CInstance other)
    {
        while (client.ReadMessage() is { } incomingMessage)
            ReadMessage(incomingMessage);
    }

    private static void ReadMessage(NetIncomingMessage incomingMessage)
    {
        switch (incomingMessage.MessageType)
        {
            case NetIncomingMessageType.Data:
                ReadDataMessage(incomingMessage);
                break;
            case NetIncomingMessageType.StatusChanged:
                Console.WriteLine(incomingMessage.ReadString());
                break;
            default:
                Console.WriteLine(incomingMessage.MessageType);
                break;
        }
    }

    private static void ReadDataMessage(NetBuffer incomingMessage)
    {
        short extensionId = incomingMessage.ReadInt16();
        if (extensionId != 0)
        {
            Console.WriteLine("oh no");
            // TODO: Other extensions lmao
            return;
        }

        MessageType messageId = (MessageType)incomingMessage.ReadInt16();
        switch (messageId)
        {
            case MessageType.PlayerConnected:
                short playerCount = incomingMessage.ReadInt16();
                for (int i = 0; i < playerCount; i++)
                    if (PlayerData.TryReadFrom(incomingMessage, out PlayerData? playerData))
                        players[playerData.id] = playerData;
                break;
            default:
                Console.WriteLine((short)messageId);
                break;
        }
    }

    [GmlInterop("mp_sendMovement")]
    public static void SendMovement(ref CInstance self, ref CInstance other, int lookDir, int room)
    {
        if (client.ConnectionStatus != NetConnectionStatus.Connected)
            return;

        NetOutgoingMessage outgoingMessage = client.CreateMessage();
        outgoingMessage.Write((short)0);
        outgoingMessage.Write(id.ToByteArray());
        outgoingMessage.Write((short)1);
        outgoingMessage.Write(room);
        outgoingMessage.Write((double)self.x);
        outgoingMessage.Write((double)self.y);
        outgoingMessage.Write((double)self.hSpeed);
        outgoingMessage.Write((double)self.vSpeed);
        outgoingMessage.Write(lookDir == -1);
        client.SendMessage(outgoingMessage, NetDeliveryMethod.UnreliableSequenced);
    }
}
