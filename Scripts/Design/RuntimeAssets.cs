using Godot;
using System.Collections.Generic;

namespace Moonroot;

public sealed class RuntimeAssets
{
    private readonly Dictionary<string, Texture2D> _textures = [];

    public Texture2D this[string key] => _textures[key];

    public void LoadAll()
    {
        Load("title", "res://assets/moonroot-title.png");
        Load("room.combat", "res://assets/environments/spring/combat-room.png");
        Load("room.boss", "res://assets/environments/spring/boss-room.png");
        Load("room.shop", "res://assets/environments/spring/shop-room.png");
        Load("room.greenhouse", "res://assets/environments/spring/greenhouse-room.png");

        Load("player", "res://assets/characters/playable/laiya.png");
        Load("atlas.npcs", "res://assets/characters/npcs/camp-npc-atlas.png");
        Load("enemy.sprout", "res://assets/characters/enemies/spring/mud-sprout.png");
        Load("enemy.radish", "res://assets/characters/enemies/spring/spike-radish.png");
        Load("enemy.beetle", "res://assets/characters/enemies/spring/shell-bean-beetle.png");
        Load("enemy.thief", "res://assets/characters/enemies/spring/seed-thief-mouse.png");
        Load("enemy.elite", "res://assets/characters/enemies/spring/moss-crowned-spike-radish.png");
        Load("boss.spring", "res://assets/characters/bosses/lantern-pumpkin-king.png");

        Load("atlas.plants", "res://assets/plants/spring-seed-growth-atlas.png");
        Load("atlas.weapons", "res://assets/weapons/spring-weapon-atlas.png");
        Load("atlas.relics", "res://assets/items/spring-relic-icons-atlas.png");
        Load("atlas.projectiles", "res://assets/vfx/projectiles/projectile-atlas.png");
        Load("atlas.melee", "res://assets/vfx/melee/melee-atlas.png");
        Load("atlas.laser", "res://assets/vfx/laser/laser-atlas.png");
        Load("atlas.impacts", "res://assets/vfx/impacts/combat-impact-atlas.png");
        Load("atlas.ecology", "res://assets/vfx/ecology/ecology-vfx-atlas.png");
        Load("atlas.telegraph", "res://assets/vfx/telegraphs/telegraph-atlas.png");
        Load("atlas.pickups", "res://assets/items/pickup-atlas.png");
        Load("atlas.interact", "res://assets/interactables/common/interactable-atlas.png");
        Load("atlas.traps", "res://assets/interactables/traps/spring-traps.png");
        Load("atlas.hud", "res://assets/ui/hud-components.png");
        Load("atlas.menu", "res://assets/ui/menu-components.png");
        Load("atlas.icons", "res://assets/ui/ui-icons.png");
        Load("atlas.network", "res://assets/ui/moonroot-network-ui-atlas.png");
        Load("atlas.challenge", "res://assets/ui/challenge-status-atlas.png");
    }

    public bool TryGet(string key, out Texture2D texture) => _textures.TryGetValue(key, out texture!);

    private void Load(string key, string path)
    {
        Texture2D? texture = GD.Load<Texture2D>(path);
        if (texture != null)
            _textures[key] = texture;
        else
            GD.PushError($"Required runtime asset failed to load: {path}");
    }
}
