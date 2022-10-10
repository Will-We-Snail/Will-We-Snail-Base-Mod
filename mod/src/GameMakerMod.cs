using System.Diagnostics.CodeAnalysis;

using GmmlInteropGenerator;
using GmmlInteropGenerator.Types;
using GmmlHooker;
using GmmlPatcher;
using UndertaleModLib;
using Lidgren.Network;
using UndertaleModLib.Models;

namespace GmmlExampleMod;

// see https://github.com/cgytrus/WysApi/WysExampleMod for more examples
// ReSharper disable once UnusedType.Global
[EnableSimpleGmlInterop]
public partial class ExampleMod : IGameMakerMod
{
    static Dictionary<Guid, CPlayerData> playerData = new Dictionary<Guid, CPlayerData>();
    static NetPeerConfiguration config = new NetPeerConfiguration("Will We Snail?");
    static NetConnectionStatus previousStatus = NetConnectionStatus.None;
    static NetClient client = new NetClient(config);
    static Guid guid = Guid.NewGuid();
    public void Load(int audioGroup, UndertaleData data, ModData currentMod)
    {
        if (audioGroup != 0) return;
        data.HookCode("gml_Object_obj_player_Step_2", "if(global.mpActive){\nsendMovement(lookdir,room)\n}\n#orig#()");
        UndertaleGameObject multiplayerManager = new UndertaleGameObject();
        multiplayerManager.Name = data.Strings.MakeString("obj_mp_manager");
        data.GameObjects.Add(multiplayerManager);
        multiplayerManager.Persistent = true;
        multiplayerManager.EventHandlerFor(EventType.Create, data.Strings, data.Code, data.CodeLocals)
        .AppendGmlSafe("show_debug_message(\"Multiplayer Object Created\")", data);
        multiplayerManager.EventHandlerFor(EventType.Step, EventSubtypeStep.Step, data).AppendGmlSafe("if(!global.mpActive){\nreturn 0\n}\nmultiplayerManager()\nupdateMultiplayerPlayers()", data);
        multiplayerManager.EventHandlerFor(EventType.KeyRelease, EventSubtypeKey.vk_f5, data).AppendGmlSafe("if(global.mpActive==false){global.mpActive=true\nmultiplayerConnect(get_string(\"IP\",\"127.0.0.1\"),get_integer(\"Port\",42069))}else{global.mpActive=false\nmultiplayerDisconnect()}", data);
        UndertaleGameObject multiplayerPlayer = new UndertaleGameObject();
        multiplayerPlayer.Name = data.Strings.MakeString("obj_mp_player");
        multiplayerPlayer.EventHandlerFor(EventType.Create, data.Strings, data.Code, data.CodeLocals).AppendGmlSafe("guid=\"0\"", data);
        multiplayerPlayer.EventHandlerFor(EventType.Step, EventSubtypeStep.Step, data).AppendGmlSafe(@"
        
        ", data);
        data.GameObjects.Add(multiplayerPlayer);
        data.CreateLegacyScript("updateMultiplayerPlayers", @"
        playerCount = mp_getPlayerCount()
        for(i = 0; i< playerCount; i++)
        {
            guid = mp_getPlayerGuid(i)
            ds_map_set(global.mpDataPosX, guid, mp_getPlayerPosX(guid))
            ds_map_set(global.mpDataPosY, guid, mp_getPlayerPosY(guid))
            ds_map_set(global.mpDataVelX, guid, mp_getPlayerVelX(guid))
            ds_map_set(global.mpDataVelY, guid, mp_getPlayerVelY(guid))
            ds_map_set(global.mpDataLookdir, guid, mp_getPlayerLookdir(guid))
            ds_map_set(global.mpDataRoom, guid, mp_getPlayerRoom(guid))
        }
        mpsnails = ds_list_create()
        for (i = 0; i < instance_number(obj_mp_player); i++){
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

        try
        {
            data.Code.First(code => code.Name.Content == "gml_Object_obj_epilepsy_warning_Create_0").AppendGmlSafe("global.mpActive=false\ninstance_create_layer(0,0,layer_create(0),obj_mp_manager)\nglobal.mpDataPosX = ds_map_create()\nglobal.mpDataPosY = ds_map_create()\nglobal.mpDataVelX = ds_map_create()\nglobal.mpDataVelY = ds_map_create()\nglobal.mpDataLookdir = ds_map_create()\nglobal.mpDataRoom = ds_map_create()", data);
        }
        catch { }
    }

    [GmlInterop("multiplayerDisconnect")]
    public static void multiplayerDisconnect(ref CInstance self, ref CInstance other)
    {
        client.Disconnect("Leaving");
        Console.WriteLine("Disconnected from multiplayer");
    }

    [GmlInterop("mp_getPlayerCount")]
    public static double mp_getPlayerCount(ref CInstance self, ref CInstance other)
    {
        return playerData.Count;
    }
    [GmlInterop("mp_getPlayerGuid")]
    public static string mp_getPlayerGuid(ref CInstance self, ref CInstance other, int position)
    {
        return playerData.Keys.ToList()[position].ToString();
    }
    [GmlInterop("mp_getPlayerPosX")]
    public static double mp_getPlayerPosX(ref CInstance self, ref CInstance other, string guid)
    {
        return playerData[new Guid(guid)].posX;
    }
    [GmlInterop("mp_getPlayerPosY")]
    public static double mp_getPlayerPosY(ref CInstance self, ref CInstance other, string guid)
    {
        return playerData[new Guid(guid)].posY;
    }
    [GmlInterop("mp_getPlayerVelX")]
    public static double mp_getPlayerVelX(ref CInstance self, ref CInstance other, string guid)
    {
        return playerData[new Guid(guid)].hSpeed;
    }
    [GmlInterop("mp_getPlayerVelY")]
    public static double mp_getPlayerVelY(ref CInstance self, ref CInstance other, string guid)
    {
        return playerData[new Guid(guid)].vSpeed;
    }
    [GmlInterop("mp_getPlayerLookdir")]
    public static int mp_getPlayerLookdir(ref CInstance self, ref CInstance other, string guid)
    {
        if (playerData[new Guid(guid)].lookDir)
        {
            return 1;
        }
        return -1;
    }
    [GmlInterop("mp_getPlayerRoom")]
    public static double mp_getPlayerRoom(ref CInstance self, ref CInstance other, string guid)
    {
        return playerData[new Guid(guid)].room;
    }

    [GmlInterop("multiplayerConnect")]
    public static void multiplayerConnect(ref CInstance self, ref CInstance other, string ip, int port)
    {
        client.Start();
        client.Connect(ip, port);
        Console.WriteLine("Started connection.");
    }

    public class CPlayerData
    {
        public Guid guid;
        public Double posX;
        public Double posY;
        public Boolean lookDir;
        public Int32 room;
        public double hSpeed;
        public double vSpeed;
        public CPlayerData(Guid guid, Double posX, Double posY, Double velX, Double velY, Boolean lookDir, Int32 room)
        {
            this.guid = guid;
            this.posX = posX;
            this.posY = posY;
            this.hSpeed = velX;
            this.vSpeed = velY;
            this.lookDir = lookDir;
            this.room = room;
        }
    }



    [GmlInterop("multiplayerManager")]
    public static void multiplayerManager(ref CInstance self, ref CInstance other)
    {
        NetIncomingMessage incomingMessage;
        while ((incomingMessage = client.ReadMessage()) != null)
        {
            switch (incomingMessage.MessageType)
            {
                case NetIncomingMessageType.Data:
                    Int16 extensionID = incomingMessage.ReadInt16();
                    if (extensionID == 0)
                    {
                        Int16 messageId = incomingMessage.ReadInt16();
                        switch (messageId)
                        {
                            case 1:
                                Int16 playerCount = incomingMessage.ReadInt16();
                                for (int playerNumber = 0; playerNumber < playerCount; playerNumber++)
                                {
                                    Guid playerGuid = new Guid(incomingMessage.ReadBytes(16));
                                    Int32 playerRoom = incomingMessage.ReadInt32();
                                    Double playerX = incomingMessage.ReadDouble();
                                    Double playerY = incomingMessage.ReadDouble();
                                    Double playerXVel = incomingMessage.ReadDouble();
                                    Double playerYVel = incomingMessage.ReadDouble();
                                    Boolean playerLookDir = incomingMessage.ReadBoolean();
                                    if (playerGuid == guid)
                                    {
                                        break;
                                    }
                                    playerData[playerGuid] = new CPlayerData(playerGuid, playerX, playerY, playerXVel, playerYVel, playerLookDir, playerRoom);
                                    //Console.WriteLine($"Player ID: {playerGuid}\nPlayer Room: {playerRoom}\n Player X: {playerX}\n Player Y: {playerY}\n Player X Vel: {playerXVel}\n Player Y Vel: {playerYVel}\n Player Look Dir: {playerLookDir}");
                                }
                                break;
                            default:
                                Console.WriteLine(messageId);
                                break;
                        }

                    }
                    else
                    {
                        Console.WriteLine("oh no");
                        // TODO: Other extensions lmao
                    }
                    break;
                case NetIncomingMessageType.StatusChanged:

                    Console.WriteLine(incomingMessage.ReadString());
                    break;
                default:
                    Console.WriteLine(incomingMessage.MessageType);
                    break;
            }
        }
    }


    [GmlInterop("sendMovement")]
    public static void sendMovement(ref CInstance self, ref CInstance other, int LookDir, int room)
    {
        if (client.ConnectionStatus == NetConnectionStatus.Connected)
        {
            NetOutgoingMessage outgoingMessage = client.CreateMessage();
            outgoingMessage.Write((Int16)0);
            outgoingMessage.Write(guid.ToByteArray());
            outgoingMessage.Write((Int16)1);
            outgoingMessage.Write((Int32)room);
            outgoingMessage.Write((Double)self.x);
            outgoingMessage.Write((Double)self.y);
            outgoingMessage.Write((Double)self.hSpeed);
            outgoingMessage.Write((Double)self.vSpeed);
            outgoingMessage.Write((Boolean)(LookDir == -1));
            client.SendMessage(outgoingMessage, NetDeliveryMethod.UnreliableSequenced);
        }

    }
}