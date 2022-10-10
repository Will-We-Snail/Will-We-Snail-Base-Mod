
using System.Diagnostics.CodeAnalysis;
using Lidgren.Network;

namespace WillWeSnail;

public record PlayerData(Guid id, int room, double posX, double posY, double hSpeed, double vSpeed, bool lookDir) {
    public static bool TryReadFrom(NetBuffer buffer, [NotNullWhen(true)] out PlayerData? playerData)
    {
        playerData = null;

        Guid id = new(buffer.ReadBytes(16));
        int room = buffer.ReadInt32();
        double posX = buffer.ReadDouble();
        double posy = buffer.ReadDouble();
        double hSpeed = buffer.ReadDouble();
        double vSpeed = buffer.ReadDouble();
        bool lookDir = buffer.ReadBoolean();

        if(id == MultiplayerMod.id)
            return false;

        playerData = new PlayerData(id, room, posX, posy, hSpeed, vSpeed, lookDir);
        return true;
    }
}