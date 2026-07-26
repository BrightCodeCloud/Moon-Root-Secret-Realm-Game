using System;
using System.Collections.Generic;
using System.Linq;

namespace Moonroot;

public enum GameScreen
{
    Title,
    Loadout,
    Playing,
    Map,
    HarvestChoice,
    Reward,
    Shop,
    Greenhouse,
    Pause,
    Result
}

public enum RoomType
{
    Entrance,
    Combat,
    Greenhouse,
    Treasure,
    Event,
    Shop,
    Boss,
    Elite,
    Hidden
}

public enum RoomWeather
{
    Gloom,
    Rain,
    MoonGap
}

public enum RootTag
{
    None,
    Wet,
    Burning,
    Spore,
    Rooted,
    Moonlit,
    Corrupted,
    Harvest,
    Attached
}

public enum WeaponType
{
    SproutStaff,
    MoonSickle,
    SunWaterer
}

public enum SeedType
{
    Pea,
    Chili,
    Pumpkin,
    Dandelion
}

public enum EnemyType
{
    MudSprout,
    SpikeRadish,
    ShellBeetle,
    SeedThief,
    EliteRadish,
    LanternPumpkinKing
}

public enum SoilType
{
    Normal,
    Wet,
    Fertile,
    Moonlit,
    Corrupted
}

public sealed class DesignRoom
{
    public int Id { get; set; }
    public RoomType Type { get; set; }
    public RoomWeather Weather { get; set; }
    public int MapX { get; set; }
    public int MapY { get; set; }
    public List<int> Connections { get; set; } = [];
    public bool Discovered { get; set; }
    public bool Cleared { get; set; }
    public bool RewardClaimed { get; set; }
    public bool BossRevealed { get; set; }
    public bool RootPurified { get; set; }
    public RootTag RootTag { get; set; }
    public string Contract { get; set; } = "";
    public int EncounterSeed { get; set; }
    public int RewardSeed { get; set; }
}

public sealed class DesignMap
{
    public int CurrentRoomId { get; set; }
    public List<DesignRoom> Rooms { get; set; } = [];

    public DesignRoom Current => Room(CurrentRoomId);
    public DesignRoom Room(int id) => Rooms.First(room => room.Id == id);
    public IEnumerable<DesignRoom> Adjacent() => Current.Connections.Select(Room);

    public void RevealFromCurrent()
    {
        Current.Discovered = true;
        foreach (int neighborId in Current.Connections)
        {
            DesignRoom neighbor = Room(neighborId);
            if (neighbor.Type == RoomType.Boss)
                neighbor.BossRevealed = true;
        }
    }
}

public static class SpringMapFactory
{
    // This is the topology shown in “多房间关卡与战斗扩展设计方案” §3.2:
    //
    //                         [Treasure]
    //                             |
    // [Entrance]—[Combat]—[Greenhouse]—[Combat]—[Unknown]
    //                |                         |        |
    //              [Event]——[Combat]——[Shop]——[Boss]
    //                            |
    //                          [Elite]—[Hidden]
    public static DesignMap Create(ulong runSeed)
    {
        Random random = new(unchecked((int)(runSeed ^ 0x5EEDBEEFUL)));
        DesignMap map = new() { CurrentRoomId = 0 };

        Add(map, 0, RoomType.Entrance, 0, 2);
        Add(map, 1, RoomType.Combat, 1, 2);
        Add(map, 2, RoomType.Greenhouse, 2, 2);
        Add(map, 3, RoomType.Combat, 3, 2);
        Add(map, 4, RoomType.Treasure, 4, 2);
        Add(map, 5, RoomType.Event, 1, 3);
        Add(map, 6, RoomType.Combat, 2, 3);
        Add(map, 7, RoomType.Shop, 3, 3);
        Add(map, 8, RoomType.Boss, 4, 3);
        Add(map, 9, RoomType.Elite, 2, 4);
        Add(map, 10, RoomType.Hidden, 3, 4);
        Add(map, 11, RoomType.Treasure, 3, 1);

        Connect(map, 0, 1);
        Connect(map, 1, 2);
        Connect(map, 1, 5);
        Connect(map, 2, 3);
        Connect(map, 3, 4);
        Connect(map, 3, 7);
        Connect(map, 3, 11);
        Connect(map, 4, 8);
        Connect(map, 5, 6);
        Connect(map, 6, 7);
        Connect(map, 6, 9);
        Connect(map, 7, 8);
        Connect(map, 9, 10);

        foreach (DesignRoom room in map.Rooms)
        {
            room.EncounterSeed = random.Next();
            room.RewardSeed = random.Next();
            room.Weather = RollWeather(random, room);
        }

        // The opening forecast must contain rain and must not begin with Moon Gap.
        map.Room(1).Weather = RoomWeather.Rain;
        map.Room(0).Weather = RoomWeather.Gloom;
        map.Room(0).Discovered = true;
        map.Room(0).Cleared = true;
        map.RevealFromCurrent();
        return map;
    }

    private static RoomWeather RollWeather(Random random, DesignRoom room)
    {
        if (room.Type is RoomType.Entrance or RoomType.Shop or RoomType.Greenhouse)
            return RoomWeather.Gloom;
        double roll = random.NextDouble();
        return roll < 0.35 ? RoomWeather.Rain : roll < 0.50 ? RoomWeather.MoonGap : RoomWeather.Gloom;
    }

    private static void Add(DesignMap map, int id, RoomType type, int x, int y)
    {
        map.Rooms.Add(new DesignRoom { Id = id, Type = type, MapX = x, MapY = y });
    }

    private static void Connect(DesignMap map, int a, int b)
    {
        map.Room(a).Connections.Add(b);
        map.Room(b).Connections.Add(a);
    }
}

public static class DesignNames
{
    public static string Room(RoomType type) => type switch
    {
        RoomType.Entrance => "入口",
        RoomType.Combat => "战斗",
        RoomType.Greenhouse => "温室",
        RoomType.Treasure => "宝藏",
        RoomType.Event => "事件",
        RoomType.Shop => "商店",
        RoomType.Boss => "Boss",
        RoomType.Elite => "精英",
        RoomType.Hidden => "隐藏",
        _ => "未知"
    };

    public static string Weather(RoomWeather weather) => weather switch
    {
        RoomWeather.Rain => "小雨",
        RoomWeather.MoonGap => "月隙",
        _ => "晴暗"
    };

    public static string Root(RootTag tag) => tag switch
    {
        RootTag.Wet => "湿润",
        RootTag.Burning => "燃烧",
        RootTag.Spore => "孢子",
        RootTag.Rooted => "扎根",
        RootTag.Moonlit => "月照",
        RootTag.Corrupted => "腐化",
        RootTag.Harvest => "收割",
        RootTag.Attached => "附着",
        _ => "无"
    };

    public static string Weapon(WeaponType weapon) => weapon switch
    {
        WeaponType.MoonSickle => "月牙镰",
        WeaponType.SunWaterer => "日棱喷壶",
        _ => "芽枝杖"
    };

    public static string Seed(SeedType seed) => seed switch
    {
        SeedType.Chili => "辣椒种",
        SeedType.Pumpkin => "南瓜种",
        SeedType.Dandelion => "蒲公英种",
        _ => "豌豆种"
    };
}
