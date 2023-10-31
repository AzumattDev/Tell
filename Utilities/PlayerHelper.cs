using System.Linq;

namespace Tell.Utilities;

public struct PlayerHelper
{
    public static PlayerHelper fromPlayerId(long id) => ZNet.instance.m_players.Where(p => p.m_characterID.UserID == id).Select(fromPlayerInfo).FirstOrDefault();
    public static PlayerHelper fromPlayerInfo(ZNet.PlayerInfo playerInfo) => new() { peerId = playerInfo.m_characterID.UserID, name = playerInfo.m_name ?? "" };
    public static PlayerHelper fromPlayer(Player player) => player == Player.m_localPlayer ? new PlayerHelper { peerId = ZDOMan.GetSessionID(), name = Game.instance.GetPlayerProfile().GetName() } : fromPlayerInfo(ZNet.instance.m_players.FirstOrDefault(info => info.m_characterID == player.GetZDOID()));

    public long peerId;
    public string name;

    public static bool operator !=(PlayerHelper a, PlayerHelper b) => !(a == b);
    public static bool operator ==(PlayerHelper a, PlayerHelper b) => a.peerId == b.peerId && a.name == b.name;
    public bool Equals(PlayerHelper other) => this == other;
    public override bool Equals(object? obj) => obj is PlayerHelper other && Equals(other);

    // ReSharper disable NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => (peerId.GetHashCode() * 397) ^ (name?.GetHashCode() ?? 0);
    // ReSharper restore NonReadonlyMemberInGetHashCode

    public override string ToString() => $"{peerId}:{name}";

    public static PlayerHelper fromString(string str)
    {
        string[] parts = str.Split(':');
        return new PlayerHelper { peerId = long.Parse(parts[0]), name = parts[1] };
    }
}