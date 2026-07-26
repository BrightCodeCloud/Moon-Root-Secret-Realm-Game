using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moonroot;

public partial class Main : Node2D
{
    private const float Width = 480;
    private const float Height = 270;
    private static readonly Rect2 Arena = new(28, 35, 424, 205);

    private sealed class Enemy
    {
        public EnemyType Type;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Health;
        public float MaxHealth;
        public float AttackTimer;
        public float StateTimer;
        public float Phase;
        public float Flash;
        public bool Charging;
    }

    private sealed class Projectile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Damage;
        public float Life;
        public float Radius;
        public int AtlasIndex;
        public bool Friendly;
        public bool Homing;
        public bool Heavy;
    }

    private sealed class Plant
    {
        public SeedType Type;
        public Vector2 Position;
        public float Growth;
        public float Age;
        public float AttackTimer;
        public bool Mature;
    }

    private sealed class SoilPatch
    {
        public SoilType Type;
        public Vector2 Position;
        public float Radius;
    }

    private sealed class Pickup
    {
        public int AtlasIndex;
        public Vector2 Position;
        public float Life = 12;
    }

    private sealed class Fx
    {
        public string Atlas = "atlas.impacts";
        public int Row;
        public Vector2 Position;
        public float Life = 0.35f;
        public float MaxLife = 0.35f;
        public float Size = 34;
    }

    private sealed class Trap
    {
        public int Kind;
        public Vector2 Position;
        public float Phase;
        public float Cooldown;
    }

    private readonly RuntimeAssets _assets = new();
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<Enemy> _enemies = [];
    private readonly List<Projectile> _projectiles = [];
    private readonly List<Plant> _plants = [];
    private readonly List<SoilPatch> _soil = [];
    private readonly List<Pickup> _pickups = [];
    private readonly List<Fx> _effects = [];
    private readonly List<Trap> _traps = [];
    private readonly HashSet<string> _relics = [];
    private readonly HashSet<string> _recipes = [];

    private GameScreen _screen = GameScreen.Title;
    private DesignMap _map = new();
    private Font? _font;
    private AudioStreamPlayer? _music;
    private ulong _runSeed;
    private float _time;
    private float _playTime;
    private float _playerHealth = 6;
    private float _playerMaxHealth = 6;
    private Vector2 _playerPosition = new(240, 185);
    private Vector2 _playerVelocity;
    private Vector2 _aimDirection = Vector2.Right;
    private float _invulnerability;
    private float _rollTime;
    private float _rollCooldown;
    private float _attackCooldown;
    private float _dewCooldown;
    private float _weaponHeat;
    private float _weaponEffect;
    private int _seedPods = 3;
    private int _maxSeedPods = 3;
    private int _moonDew;
    private int _seasonLeaves;
    private int _difficulty = 1;
    private int _routeIndex;
    private int _choiceIndex;
    private int _roomsCleared;
    private int _score;
    private int _combatRoomsScored;
    private int _roomPlantsPlaced;
    private int _roomPlantsMatured;
    private int _roomHarvested;
    private int _roomRootsLeft;
    private int _roomDamageTaken;
    private bool _roomClear;
    private bool _roomRewardPending;
    private bool _roomFirstHarvest;
    private bool _bossDefeated;
    private WeaponType _weapon = WeaponType.SproutStaff;
    private SeedType _seed = SeedType.Pea;
    private string _message = "";
    private float _messageTimer;
    private string _contract = "";
    private string _capturePath = "";
    private string _captureScene = "battle";
    private bool _automation;
    private float _automationTimer;

    private static readonly Color Deep = Color.FromHtml("#0E1628");
    private static readonly Color DeepBlue = Color.FromHtml("#1D2D44");
    private static readonly Color Moss = Color.FromHtml("#496B3B");
    private static readonly Color Sprout = Color.FromHtml("#88B84B");
    private static readonly Color SoilBrown = Color.FromHtml("#6B4632");
    private static readonly Color Wood = Color.FromHtml("#9A6139");
    private static readonly Color Honey = Color.FromHtml("#E6A84A");
    private static readonly Color Pumpkin = Color.FromHtml("#D96832");
    private static readonly Color Health = Color.FromHtml("#C84C4C");
    private static readonly Color Cyan = Color.FromHtml("#3BC6C4");
    private static readonly Color Parchment = Color.FromHtml("#F3DEB3");
    private static readonly Color MoonViolet = Color.FromHtml("#9C79D6");

    private DesignRoom CurrentRoom => _map.Current;
    private bool IsCombatRoom => CurrentRoom.Type is RoomType.Combat or RoomType.Elite or RoomType.Boss;

    public override void _Ready()
    {
        _assets.LoadAll();
        _font = ThemeDB.FallbackFont;
        DisplayServer.WindowSetTitle("月根秘境 · 春季垂直切片");
        InitializeMusic();
        PlayMusic("res://assets/audio/music/menu.ogg");

        string[] args = OS.GetCmdlineUserArgs();
        _automation = args.Contains("--design-smoke-test");
        string? captureArg = args.FirstOrDefault(arg => arg.StartsWith("--capture=", StringComparison.Ordinal));
        if (captureArg != null)
            _capturePath = captureArg["--capture=".Length..];
        string? captureSceneArg = args.FirstOrDefault(arg => arg.StartsWith("--capture-scene=", StringComparison.Ordinal));
        if (captureSceneArg != null)
            _captureScene = captureSceneArg["--capture-scene=".Length..];

        if (_automation || !string.IsNullOrEmpty(_capturePath))
        {
            _weapon = WeaponType.SproutStaff;
            _seed = SeedType.Pea;
            if (_captureScene == "loadout")
            {
                _screen = GameScreen.Loadout;
            }
            else
            {
                BeginRun();
                EnterRoom(_captureScene == "boss" ? 8 : 1);
                _playerHealth = 1000;
                _playerMaxHealth = 1000;
                AddPlant(SeedType.Pea, new Vector2(180, 170), true);
                AddPlant(SeedType.Pumpkin, new Vector2(305, 168), true);
                if (_captureScene == "map")
                {
                    foreach (DesignRoom room in _map.Rooms)
                        room.Discovered = true;
                    _map.Room(1).RootTag = RootTag.Wet;
                    _map.Room(2).RootTag = RootTag.Spore;
                    _map.Room(3).RootTag = RootTag.Attached;
                    _map.Room(8).BossRevealed = true;
                    OpenMap();
                }
            }
        }

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = Math.Min((float)delta, 0.033f);
        _time += dt;
        if (_messageTimer > 0) _messageTimer -= dt;
        if (_weaponEffect > 0) _weaponEffect -= dt;

        if (_screen == GameScreen.Playing)
        {
            _playTime += dt;
            UpdateWorld(dt);
        }

        if (_automation)
            UpdateAutomation(dt);

        if (!string.IsNullOrEmpty(_capturePath))
        {
            _automationTimer += dt;
            if (_automationTimer > 1.2f)
            {
                Error result;
                try
                {
                    Image? frame = GetViewport().GetTexture()?.GetImage();
                    result = frame == null || frame.IsEmpty() ? Error.Unavailable : frame.SavePng(_capturePath);
                }
                catch (Exception exception)
                {
                    GD.PushError($"Visual capture unavailable: {exception.Message}");
                    result = Error.Unavailable;
                }
                GD.Print(result == Error.Ok ? $"VISUAL_CAPTURE_OK path={_capturePath}" : $"VISUAL_CAPTURE_FAILED error={result}");
                _capturePath = "";
                GetTree().Quit(result == Error.Ok ? 0 : 1);
            }
        }

        QueueRedraw();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
            UpdateAimFromMouse();

        if (@event is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (_screen == GameScreen.Playing && mouse.ButtonIndex == MouseButton.Right)
                TryPlant(GetLocalMousePosition());
            else if (mouse.ButtonIndex == MouseButton.Left)
                HandleClick(mouse.Position);
            else if (_screen == GameScreen.Playing && mouse.ButtonIndex == MouseButton.WheelUp)
                CycleSeed(-1);
            else if (_screen == GameScreen.Playing && mouse.ButtonIndex == MouseButton.WheelDown)
                CycleSeed(1);
        }

        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        if (_screen == GameScreen.Title)
        {
            if (key.Keycode is Key.Enter or Key.KpEnter)
                _screen = GameScreen.Loadout;
            if (key.Keycode == Key.Escape) GetTree().Quit();
            return;
        }

        if (_screen == GameScreen.Loadout)
        {
            if (key.Keycode is Key.Left or Key.A) CycleWeapon(-1);
            if (key.Keycode is Key.Right or Key.D) CycleWeapon(1);
            if (key.Keycode is Key.Up or Key.W) CycleSeed(-1);
            if (key.Keycode is Key.Down or Key.S) CycleSeed(1);
            if (key.Keycode == Key.K) _difficulty = Wrap(_difficulty + 1, 3);
            if (key.Keycode is Key.Enter or Key.KpEnter) BeginRun();
            if (key.Keycode == Key.Escape) _screen = GameScreen.Title;
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            if (_screen == GameScreen.Playing) _screen = GameScreen.Pause;
            else if (_screen == GameScreen.Pause) _screen = GameScreen.Playing;
            else if (_screen == GameScreen.Map) _screen = GameScreen.Playing;
            else _screen = GameScreen.Map;
            return;
        }

        if (_screen == GameScreen.Playing)
        {
            if (key.Keycode == Key.Space) TryRoll();
            if (key.Keycode == Key.Q) UseDewRing();
            if (key.Keycode == Key.Tab) OpenMap();
            if (key.Keycode == Key.E && _roomClear) ResolveRoomExit();
            if (key.Keycode == Key.R) CycleWeapon(1);
            if (key.Keycode is >= Key.Key1 and <= Key.Key4)
                _seed = (SeedType)((int)key.Keycode - (int)Key.Key1);
            return;
        }

        if (_screen == GameScreen.Map)
        {
            List<DesignRoom> routes = TravelOptions();
            if (key.Keycode is Key.Left or Key.A) _routeIndex = Wrap(_routeIndex - 1, routes.Count);
            if (key.Keycode is Key.Right or Key.D) _routeIndex = Wrap(_routeIndex + 1, routes.Count);
            if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space) TravelSelected();
            if (key.Keycode == Key.K) CycleContract();
            return;
        }

        if (_screen is GameScreen.HarvestChoice or GameScreen.Reward or GameScreen.Shop or GameScreen.Greenhouse)
        {
            if (key.Keycode is >= Key.Key1 and <= Key.Key3)
                ResolveChoice((int)key.Keycode - (int)Key.Key1);
            if (key.Keycode == Key.E && _screen is GameScreen.Shop or GameScreen.Greenhouse)
                OpenMap();
            return;
        }

        if (_screen == GameScreen.Pause && key.Keycode == Key.Tab)
            _screen = GameScreen.Map;
        if (_screen == GameScreen.Result && key.Keycode is Key.Enter or Key.KpEnter)
            _screen = GameScreen.Title;
    }

    private void BeginRun()
    {
        _runSeed = (ulong)Time.GetTicksMsec();
        _rng.Seed = _runSeed;
        _map = SpringMapFactory.Create(_runSeed);
        _playerHealth = _playerMaxHealth = 6;
        _seedPods = _maxSeedPods = 3;
        _moonDew = 0;
        _seasonLeaves = 0;
        _score = 0;
        _roomsCleared = 0;
        _combatRoomsScored = 0;
        _relics.Clear();
        _recipes.Clear();
        _bossDefeated = false;
        PlayMusic("res://assets/audio/music/spring.ogg");
        EnterRoom(0);
        ShowMessage("春·苔灯地窖  天气预报已记录");
    }

    private void EnterRoom(int roomId)
    {
        _map.CurrentRoomId = roomId;
        _map.RevealFromCurrent();
        _enemies.Clear();
        _projectiles.Clear();
        _plants.Clear();
        _soil.Clear();
        _pickups.Clear();
        _effects.Clear();
        _traps.Clear();
        _playerPosition = new Vector2(240, 190);
        _playerVelocity = Vector2.Zero;
        _invulnerability = 1;
        _roomDamageTaken = 0;
        _roomPlantsPlaced = 0;
        _roomPlantsMatured = 0;
        _roomHarvested = 0;
        _roomRootsLeft = 0;
        _roomRewardPending = false;
        _roomFirstHarvest = true;
        _contract = CurrentRoom.Contract;
        _roomClear = CurrentRoom.Cleared || !IsCombatRoom;
        if (!IsCombatRoom)
            CurrentRoom.Cleared = true;

        ConfigureRoomSoil();
        if (IsCombatRoom && !CurrentRoom.Cleared)
            SpawnEncounter();

        _screen = CurrentRoom.Type switch
        {
            RoomType.Shop => GameScreen.Shop,
            RoomType.Greenhouse => GameScreen.Greenhouse,
            _ => GameScreen.Playing
        };

        if (CurrentRoom.Type == RoomType.Event && !CurrentRoom.RewardClaimed)
            ResolveEvent();
        if (CurrentRoom.Type == RoomType.Treasure && !CurrentRoom.RewardClaimed)
            OpenReward();
        if (CurrentRoom.Type == RoomType.Hidden && !CurrentRoom.RewardClaimed)
            OpenReward();
        if (CurrentRoom.Type == RoomType.Entrance)
            _roomClear = true;
    }

    private void ConfigureRoomSoil()
    {
        if (CurrentRoom.Weather == RoomWeather.Rain)
        {
            _soil.Add(new SoilPatch { Type = SoilType.Wet, Position = new Vector2(145, 155), Radius = 34 });
            _soil.Add(new SoilPatch { Type = SoilType.Wet, Position = new Vector2(330, 175), Radius = 32 });
        }
        else if (CurrentRoom.Weather == RoomWeather.MoonGap)
        {
            _soil.Add(new SoilPatch { Type = SoilType.Moonlit, Position = new Vector2(240, 150), Radius = 38 });
        }

        foreach (DesignRoom neighbor in _map.Adjacent().Where(room => room.RootTag != RootTag.None))
        {
            switch (neighbor.RootTag)
            {
                case RootTag.Wet:
                    _soil.Add(new SoilPatch { Type = SoilType.Wet, Position = new Vector2(240, 175), Radius = 45 });
                    break;
                case RootTag.Moonlit:
                    _soil.Add(new SoilPatch { Type = SoilType.Moonlit, Position = new Vector2(350, 145), Radius = 32 });
                    break;
                case RootTag.Corrupted:
                    _soil.Add(new SoilPatch { Type = SoilType.Corrupted, Position = new Vector2(150, 175), Radius = 28 });
                    break;
                case RootTag.Rooted:
                    AddPlant(SeedType.Pumpkin, new Vector2(240, 125), true);
                    break;
            }
        }
    }

    private void SpawnEncounter()
    {
        _rng.Seed = (ulong)(uint)CurrentRoom.EncounterSeed;
        if (CurrentRoom.Type == RoomType.Boss)
        {
            SpawnEnemy(EnemyType.LanternPumpkinKing, new Vector2(240, 105));
            return;
        }

        float threat = 4 + _roomsCleared * 0.7f + (CurrentRoom.Type == RoomType.Elite ? 2.5f : 0);
        if (_difficulty == 0) threat *= 0.75f;
        if (_difficulty == 2) threat *= 1.3f;

        if (CurrentRoom.Type == RoomType.Elite)
        {
            SpawnEnemy(EnemyType.EliteRadish, new Vector2(240, 110));
            threat -= 3.5f;
        }

        EnemyType[] pool = [EnemyType.MudSprout, EnemyType.SpikeRadish, EnemyType.ShellBeetle, EnemyType.SeedThief];
        int index = 0;
        while (threat > 0.5f && _enemies.Count < 9)
        {
            EnemyType type = pool[(CurrentRoom.Id + index + _rng.RandiRange(0, 2)) % pool.Length];
            float cost = type switch { EnemyType.SpikeRadish => 1.5f, EnemyType.ShellBeetle => 2f, _ => 1f };
            if (cost > threat + 0.5f) type = EnemyType.MudSprout;
            Vector2 position = new(_rng.RandfRange(75, 405), _rng.RandfRange(75, 205));
            if (position.DistanceTo(_playerPosition) < 90) position.Y = 80;
            SpawnEnemy(type, position);
            threat -= cost;
            index++;
        }

        if (_contract == "虫潮")
        {
            SpawnEnemy(EnemyType.MudSprout, new Vector2(95, 95));
            SpawnEnemy(EnemyType.MudSprout, new Vector2(385, 95));
        }
        if (_contract == "腐土")
        {
            _soil.Add(new SoilPatch { Type = SoilType.Corrupted, Position = new Vector2(160, 150), Radius = 25 });
            _soil.Add(new SoilPatch { Type = SoilType.Corrupted, Position = new Vector2(325, 160), Radius = 25 });
        }

        int trapCount = CurrentRoom.Type == RoomType.Elite ? 3 : CurrentRoom.Type == RoomType.Boss ? 2 : 1 + CurrentRoom.Id % 2;
        for (int i = 0; i < trapCount; i++)
        {
            int kind = (CurrentRoom.Id + i) % 4;
            _traps.Add(new Trap
            {
                Kind = kind,
                Position = new Vector2(110 + i * 128, 132 + (i % 2) * 54),
                Phase = _rng.RandfRange(0, 2.4f),
                Cooldown = 1.1f + i * 0.35f
            });
        }
    }

    private void SpawnEnemy(EnemyType type, Vector2 position)
    {
        float health = type switch
        {
            EnemyType.MudSprout => 22,
            EnemyType.SpikeRadish => 28,
            EnemyType.ShellBeetle => 42,
            EnemyType.SeedThief => 24,
            EnemyType.EliteRadish => 58,
            EnemyType.LanternPumpkinKing => 1350,
            _ => 24
        };
        health *= _difficulty switch { 0 => 0.9f, 2 => 1.15f, _ => 1 };
        _enemies.Add(new Enemy
        {
            Type = type,
            Position = position,
            Health = health,
            MaxHealth = health,
            AttackTimer = _rng.RandfRange(0.8f, 1.6f),
            StateTimer = _rng.RandfRange(0.4f, 1.1f),
            Phase = _rng.RandfRange(0, Mathf.Tau)
        });
    }

    private void UpdateWorld(float dt)
    {
        _invulnerability = Math.Max(0, _invulnerability - dt);
        _rollCooldown = Math.Max(0, _rollCooldown - dt);
        _attackCooldown = Math.Max(0, _attackCooldown - dt);
        _dewCooldown = Math.Max(0, _dewCooldown - dt);

        UpdateAimFromMouse();
        UpdatePlayer(dt);
        UpdateWeapons(dt);
        UpdatePlants(dt);
        UpdateTraps(dt);
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdatePickups(dt);
        UpdateEffects(dt);

        if (!_roomClear && _enemies.Count == 0)
            CompleteRoom();
    }

    private void UpdateTraps(float dt)
    {
        foreach (Trap trap in _traps)
        {
            trap.Phase += dt;
            trap.Cooldown -= dt;
            if (trap.Cooldown > 0) continue;

            if (trap.Kind == 0 && trap.Position.DistanceTo(_playerPosition) < 30)
                HurtPlayer(trap.Position, 1);
            else if (trap.Kind == 1)
            {
                Vector2 aimed = trap.Position.DirectionTo(_playerPosition);
                SpawnProjectile(trap.Position, aimed * 92, 1, false, 8, 5);
            }
            else if (trap.Kind == 2 && trap.Position.DistanceTo(_playerPosition) < 35)
                _playerVelocity *= 0.35f;
            else if (trap.Kind == 3)
                SpawnRootTelegraph(_playerPosition);

            if (trap.Kind is 0 or 3)
            {
                foreach (Enemy enemy in _enemies.Where(enemy => enemy.Position.DistanceTo(trap.Position) < 32).ToArray())
                    DamageEnemy(enemy, 1.5f);
            }
            trap.Cooldown = 2.5f + trap.Kind * 0.35f;
        }
    }

    private void UpdatePlayer(float dt)
    {
        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;
        if (Input.IsKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Y += 1;
        input = input.LimitLength();

        if (_rollTime > 0)
        {
            _rollTime -= dt;
            _playerVelocity = _playerVelocity.MoveToward(Vector2.Zero, 330 * dt);
        }
        else
        {
            float speed = _roomClear ? 125 : 100;
            if (_relics.Contains("蜗牛时钟")) speed *= 0.92f;
            if (SoilAt(_playerPosition) == SoilType.Wet)
                speed *= _relics.Contains("不漏水的靴子") ? 1.12f : 0.95f;
            _playerVelocity = _playerVelocity.MoveToward(input * speed, 580 * dt);
        }
        _playerPosition += _playerVelocity * dt;
        _playerPosition.X = Mathf.Clamp(_playerPosition.X, Arena.Position.X + 10, Arena.End.X - 10);
        _playerPosition.Y = Mathf.Clamp(_playerPosition.Y, Arena.Position.Y + 12, Arena.End.Y - 8);
    }

    private void UpdateAimFromMouse()
    {
        Vector2 mouse = GetLocalMousePosition();
        if (mouse.DistanceTo(_playerPosition) > 4)
            _aimDirection = _playerPosition.DirectionTo(mouse);
    }

    private void UpdateWeapons(float dt)
    {
        bool firing = Input.IsMouseButtonPressed(MouseButton.Left) && _screen == GameScreen.Playing && !_roomClear;
        if (_weapon == WeaponType.SunWaterer)
        {
            if (firing && _weaponHeat < 2.5f)
            {
                _weaponHeat += dt;
                _weaponEffect = 0.08f;
                Vector2 end = _playerPosition + _aimDirection * 190;
                foreach (Enemy enemy in _enemies.ToArray())
                {
                    Vector2 closest = Geometry2D.GetClosestPointToSegment(enemy.Position, _playerPosition, end);
                    if (closest.DistanceTo(enemy.Position) < 13)
                        DamageEnemy(enemy, 28 * dt);
                }
                foreach (Plant plant in _plants)
                {
                    Vector2 closest = Geometry2D.GetClosestPointToSegment(plant.Position, _playerPosition, end);
                    if (closest.DistanceTo(plant.Position) < 15)
                        plant.Growth += dt * 0.7f;
                }
            }
            else
            {
                _weaponHeat = Math.Max(0, _weaponHeat - dt * (firing ? 0.2f : 1.35f));
            }
            return;
        }

        if (!firing || _attackCooldown > 0) return;
        if (_weapon == WeaponType.MoonSickle)
        {
            _attackCooldown = 1f / 1.4f;
            _weaponEffect = 0.22f;
            foreach (Enemy enemy in _enemies.ToArray())
            {
                Vector2 toEnemy = _playerPosition.DirectionTo(enemy.Position);
                if (enemy.Position.DistanceTo(_playerPosition) <= 42 && Math.Abs(_aimDirection.AngleTo(toEnemy)) <= Mathf.DegToRad(55))
                    DamageEnemy(enemy, 18);
            }
            foreach (Projectile projectile in _projectiles.Where(projectile => !projectile.Friendly && !projectile.Heavy).ToArray())
            {
                if (projectile.Position.DistanceTo(_playerPosition) <= 43)
                {
                    projectile.Friendly = true;
                    projectile.Velocity = -projectile.Velocity * 1.25f;
                    projectile.AtlasIndex = 0;
                }
            }
        }
        else
        {
            _attackCooldown = 0.4f;
            SpawnProjectile(_playerPosition + _aimDirection * 12, _aimDirection * 245, 10, true, 0, 4);
        }
    }

    private void UpdatePlants(float dt)
    {
        foreach (Plant plant in _plants.ToArray())
        {
            plant.Age += dt;
            SoilType soil = SoilAt(plant.Position);
            float growthRate = soil == SoilType.Wet ? 1.15f : 1f;
            if (_contract == "旱季") growthRate *= 0.75f;
            if (_relics.Contains("蜗牛时钟")) growthRate *= 1.18f;
            if (soil == SoilType.Moonlit && _relics.Contains("月下玻璃瓶")) growthRate *= 1.35f;
            if (_relics.Contains("园丁的便签") && plant.Age < 0.05f) plant.Growth += GrowthTime(plant.Type) * 0.25f;
            if (soil != SoilType.Corrupted) plant.Growth += dt * growthRate;

            if (!plant.Mature && plant.Growth >= GrowthTime(plant.Type))
            {
                plant.Mature = true;
                _roomPlantsMatured++;
                _effects.Add(new Fx { Atlas = "atlas.ecology", Row = 1, Position = plant.Position, Size = 44 });
            }

            if (!plant.Mature) continue;
            plant.AttackTimer -= dt;
            if (plant.Type == SeedType.Pea && plant.AttackTimer <= 0)
            {
                Enemy? target = NearestEnemy(plant.Position);
                if (target != null)
                    SpawnProjectile(plant.Position + new Vector2(0, -8), plant.Position.DirectionTo(target.Position) * 180, 6, true, 0, 3);
                plant.AttackTimer = 0.8f;
            }
            else if (plant.Type == SeedType.Chili)
            {
                foreach (Enemy enemy in _enemies.Where(enemy => enemy.Position.DistanceTo(plant.Position) < 45).ToArray())
                    DamageEnemy(enemy, (_relics.Contains("暖手石") ? 6.5f : 5f) * dt);
            }
            else if (plant.Type == SeedType.Pumpkin)
            {
                foreach (Enemy enemy in _enemies.Where(enemy => enemy.Position.DistanceTo(plant.Position) < 24))
                    enemy.Velocity *= 0.72f;
            }
            else if (plant.Type == SeedType.Dandelion && plant.AttackTimer <= 0)
            {
                Enemy? target = NearestEnemy(plant.Position);
                if (target != null)
                    SpawnProjectile(plant.Position, plant.Position.DirectionTo(target.Position) * 115, 7, true, 12, 4, true);
                plant.AttackTimer = 1.2f;
            }
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy enemy in _enemies.ToArray())
        {
            enemy.Phase += dt;
            enemy.AttackTimer -= dt;
            enemy.StateTimer -= dt;
            enemy.Flash = Math.Max(0, enemy.Flash - dt * 8);

            if (enemy.Type == EnemyType.LanternPumpkinKing)
            {
                UpdateBoss(enemy, dt);
                continue;
            }

            if (enemy.Type == EnemyType.MudSprout)
            {
                enemy.Velocity = enemy.Velocity.MoveToward(enemy.Position.DirectionTo(_playerPosition) * 42, 90 * dt);
                if (enemy.StateTimer <= 0)
                {
                    enemy.Velocity += enemy.Position.DirectionTo(_playerPosition) * 85;
                    enemy.StateTimer = 1.8f;
                }
            }
            else if (enemy.Type is EnemyType.SpikeRadish or EnemyType.EliteRadish)
            {
                enemy.Velocity = enemy.Velocity.MoveToward(Vector2.Zero, 120 * dt);
                if (enemy.AttackTimer <= 0)
                {
                    Vector2 aimed = enemy.Position.DirectionTo(_playerPosition);
                    for (int i = -1; i <= 1; i++)
                        SpawnProjectile(enemy.Position, aimed.Rotated(i * 0.13f) * 105, 1, false, 4, 4);
                    if (enemy.Type == EnemyType.EliteRadish)
                        SpawnRootTelegraph(_playerPosition);
                    enemy.AttackTimer = enemy.Type == EnemyType.EliteRadish ? 2.6f : 2.2f;
                }
            }
            else if (enemy.Type == EnemyType.ShellBeetle)
            {
                if (!enemy.Charging && enemy.StateTimer <= 0)
                {
                    enemy.Charging = true;
                    enemy.StateTimer = 0.8f;
                    enemy.Velocity = Vector2.Zero;
                }
                else if (enemy.Charging && enemy.StateTimer <= 0)
                {
                    enemy.Velocity = enemy.Position.DirectionTo(_playerPosition) * 165;
                    enemy.Charging = false;
                    enemy.StateTimer = 2.2f;
                }
                else if (!enemy.Charging)
                    enemy.Velocity = enemy.Velocity.MoveToward(Vector2.Zero, 80 * dt);
            }
            else if (enemy.Type == EnemyType.SeedThief)
            {
                Plant? target = _plants.Where(plant => plant.Mature).OrderBy(plant => plant.Position.DistanceSquaredTo(enemy.Position)).FirstOrDefault();
                Vector2 destination = target?.Position ?? _playerPosition;
                enemy.Velocity = enemy.Velocity.MoveToward(enemy.Position.DirectionTo(destination) * 72, 120 * dt);
                if (target != null && enemy.Position.DistanceTo(target.Position) < 16)
                {
                    _plants.Remove(target);
                    enemy.Velocity *= 1.35f;
                    ShowMessage("偷苗鼠吃掉了成熟植物！");
                }
            }

            enemy.Position += enemy.Velocity * dt;
            enemy.Position.X = Mathf.Clamp(enemy.Position.X, Arena.Position.X + 12, Arena.End.X - 12);
            enemy.Position.Y = Mathf.Clamp(enemy.Position.Y, Arena.Position.Y + 12, Arena.End.Y - 10);
            if (enemy.Type is EnemyType.MudSprout or EnemyType.ShellBeetle && enemy.Position.DistanceTo(_playerPosition) < 14)
                HurtPlayer(enemy.Position, 1);
        }
    }

    private void UpdateBoss(Enemy boss, float dt)
    {
        float ratio = boss.Health / boss.MaxHealth;
        HashSet<RootTag> inherited = CurrentRoom.Connections
            .Select(_map.Room)
            .Where(room => room.RootTag != RootTag.None)
            .Select(room => room.RootTag)
            .ToHashSet();
        Vector2 target = new(240 + Mathf.Sin(boss.Phase * 0.6f) * 105, 105 + Mathf.Sin(boss.Phase * 0.9f) * 28);
        boss.Velocity = boss.Velocity.MoveToward(boss.Position.DirectionTo(target) * (ratio < 0.3f ? 34 : 22), 70 * dt);
        boss.Position += boss.Velocity * dt;

        if (boss.AttackTimer > 0) return;
        Vector2 aimed = boss.Position.DirectionTo(_playerPosition);
        int count = ratio < 0.65f ? 7 : 5;
        for (int i = 0; i < count; i++)
        {
            float spread = Mathf.DegToRad(58);
            float angle = count == 1 ? 0 : -spread / 2 + spread * i / (count - 1);
            float speed = inherited.Contains(RootTag.Wet) ? 82 : 92;
            SpawnProjectile(boss.Position, aimed.Rotated(angle) * speed, 1, false, 15, 5, false, true);
        }
        if (inherited.Contains(RootTag.Moonlit))
            SpawnProjectile(boss.Position, aimed.Rotated(Mathf.Sin(boss.Phase) * 0.6f) * 115, 1, false, 11, 5);

        if (ratio < 0.65f)
            _soil.Add(new SoilPatch { Type = SoilType.Fertile, Position = _playerPosition, Radius = 26 });
        if (ratio < 0.3f)
        {
            for (int i = 0; i < 3; i++)
                SpawnRootTelegraph(_playerPosition + Vector2.Right.Rotated(i * Mathf.Tau / 3) * 42);
        }
        boss.AttackTimer = ratio < 0.3f ? 1.45f : ratio < 0.65f ? 1.85f : 2.2f;
    }

    private void SpawnRootTelegraph(Vector2 position)
    {
        _effects.Add(new Fx { Atlas = "atlas.telegraph", Row = 0, Position = position, Life = 1, MaxLife = 1, Size = 46 });
        SpawnProjectile(position, Vector2.Zero, 1, false, 13, 10, false, true, 0.95f);
    }

    private void UpdateProjectiles(float dt)
    {
        foreach (Projectile projectile in _projectiles.ToArray())
        {
            projectile.Life -= dt;
            if (projectile.Homing && projectile.Friendly)
            {
                Enemy? target = NearestEnemy(projectile.Position);
                if (target != null)
                    projectile.Velocity = projectile.Velocity.Lerp(projectile.Position.DirectionTo(target.Position) * projectile.Velocity.Length(), dt * 4);
            }
            projectile.Position += projectile.Velocity * dt;

            if (projectile.Life <= 0 || !new Rect2(12, 20, 456, 240).HasPoint(projectile.Position))
            {
                _projectiles.Remove(projectile);
                continue;
            }

            if (projectile.Friendly)
            {
                Enemy? hit = _enemies.FirstOrDefault(enemy => enemy.Position.DistanceTo(projectile.Position) < projectile.Radius + (enemy.Type == EnemyType.LanternPumpkinKing ? 28 : 11));
                if (hit != null)
                {
                    DamageEnemy(hit, projectile.Damage);
                    if (projectile.AtlasIndex == 0 && _relics.Contains("雨后豆荚") && SoilAt(hit.Position) == SoilType.Wet)
                    {
                        Enemy? bounce = _enemies.Where(enemy => enemy != hit)
                            .OrderBy(enemy => enemy.Position.DistanceSquaredTo(hit.Position))
                            .FirstOrDefault();
                        if (bounce != null)
                            SpawnProjectile(hit.Position, hit.Position.DirectionTo(bounce.Position) * 190, projectile.Damage * 0.75f, true, 0, projectile.Radius);
                    }
                    _projectiles.Remove(projectile);
                }
            }
            else if (projectile.Velocity.LengthSquared() < 1 && projectile.Life > 0.05f)
            {
                if (projectile.Life < 0.08f && projectile.Position.DistanceTo(_playerPosition) < 20)
                    HurtPlayer(projectile.Position, projectile.Damage);
            }
            else if (projectile.Position.DistanceTo(_playerPosition) < projectile.Radius + 7)
            {
                HurtPlayer(projectile.Position, projectile.Damage);
                _projectiles.Remove(projectile);
            }
        }
    }

    private void UpdatePickups(float dt)
    {
        foreach (Pickup pickup in _pickups.ToArray())
        {
            pickup.Life -= dt;
            if (pickup.Position.DistanceTo(_playerPosition) < 22)
            {
                if (pickup.AtlasIndex == 0) _moonDew++;
                else if (pickup.AtlasIndex == 2) _seedPods = Math.Min(_maxSeedPods, _seedPods + 1);
                else if (pickup.AtlasIndex == 3) _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 1);
                _pickups.Remove(pickup);
            }
            else if (pickup.Life <= 0)
                _pickups.Remove(pickup);
        }
    }

    private void UpdateEffects(float dt)
    {
        foreach (Fx effect in _effects.ToArray())
        {
            effect.Life -= dt;
            if (effect.Life <= 0) _effects.Remove(effect);
        }
    }

    private void CompleteRoom()
    {
        _roomClear = true;
        CurrentRoom.Cleared = true;
        _roomsCleared++;
        _roomRewardPending = true;
        _moonDew += CurrentRoom.Type == RoomType.Elite ? 10 : 5;
        if (_rng.Randf() < (_playerHealth <= 2 ? 0.35f : 0.15f))
            _pickups.Add(new Pickup { AtlasIndex = 3, Position = new Vector2(240, 125) });
        if (_rng.Randf() < (_seedPods == 0 ? 0.45f : 0.22f))
            _pickups.Add(new Pickup { AtlasIndex = 2, Position = new Vector2(265, 125) });

        int baseScore = CurrentRoom.Type == RoomType.Boss ? 1500 : CurrentRoom.Type == RoomType.Elite ? 400 : 100;
        if (CurrentRoom.Type == RoomType.Combat && _combatRoomsScored++ >= 5) baseScore = 0;
        if (_roomDamageTaken == 0) baseScore += baseScore / 4;
        baseScore += Mathf.FloorToInt(baseScore * ContractMultiplier(_contract));
        _score += baseScore;

        if (CurrentRoom.Type == RoomType.Boss)
        {
            _bossDefeated = true;
            _score += 300;
            _screen = GameScreen.Result;
            PlayMusic("res://assets/audio/music/menu.ogg");
            return;
        }

        _effects.Add(new Fx { Atlas = "atlas.impacts", Row = 3, Position = new Vector2(240, 120), Size = 58 });
        ShowMessage("房间已恢复平静 · E 处理植物并查看地图");
    }

    private void ResolveRoomExit()
    {
        if (_plants.Any(plant => plant.Mature))
        {
            _choiceIndex = 0;
            _screen = GameScreen.HarvestChoice;
        }
        else if (CurrentRoom.Type is RoomType.Combat or RoomType.Elite && _roomsCleared % 3 == 0)
        {
            OpenReward();
        }
        else
        {
            OpenMap();
        }
    }

    private void ResolveChoice(int index)
    {
        if (_screen == GameScreen.HarvestChoice)
        {
            if (index == 0) HarvestAll();
            else LeaveRoot();
            if (CurrentRoom.Type is RoomType.Combat or RoomType.Elite && _roomsCleared % 3 == 0)
                OpenReward();
            else
                OpenMap();
            return;
        }

        if (_screen == GameScreen.Reward)
        {
            string[] relics = RewardChoices();
            if (index >= relics.Length) return;
            _relics.Add(relics[index]);
            ApplyRelic(relics[index]);
            CurrentRoom.RewardClaimed = true;
            ShowMessage($"获得遗物：{relics[index]}");
            OpenMap();
            return;
        }

        if (_screen == GameScreen.Shop)
        {
            int[] prices = [8, 10, 22];
            if (index < 0 || index >= prices.Length || _moonDew < prices[index]) return;
            _moonDew -= prices[index];
            if (index == 0) _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 1);
            if (index == 1) _seedPods = Math.Min(_maxSeedPods, _seedPods + 1);
            if (index == 2)
            {
                string relic = RewardChoices()[0];
                _relics.Add(relic);
                ApplyRelic(relic);
            }
            ShowMessage("眠鼠商人的货台已经空了");
            return;
        }

        if (_screen == GameScreen.Greenhouse)
        {
            if (CurrentRoom.RewardClaimed) return;
            if (index == 0)
            {
                _seedPods = _maxSeedPods;
                ShowMessage("苔婆婆升级了当前主种子");
            }
            else if (index == 1)
            {
                DesignRoom? root = _map.Rooms.LastOrDefault(room => room.RootTag != RootTag.None);
                if (root != null) root.RootTag = RootTag.Moonlit;
                ShowMessage("最近的根忆已移栽到月照地");
            }
            else
            {
                _seasonLeaves++;
                ShowMessage("一个放弃的奖励已被封存");
            }
            CurrentRoom.RewardClaimed = true;
        }
    }

    private void HarvestAll()
    {
        int matureKinds = _plants.Where(plant => plant.Mature).Select(plant => plant.Type).Distinct().Count();
        foreach (Plant plant in _plants.Where(plant => plant.Mature).ToArray())
            HarvestPlant(plant);
        if (matureKinds >= 3 && _relics.Contains("三齿小耙"))
            _seedPods = Math.Min(_maxSeedPods, _seedPods + 1);
        _roomHarvested++;
        _moonDew += Math.Max(1, _plants.Count);
        _plants.Clear();
    }

    private void LeaveRoot()
    {
        Plant? chosen = _plants.FirstOrDefault(plant => plant.Mature);
        if (chosen == null) return;
        RootTag tag = RootTagFor(chosen);
        CurrentRoom.RootTag = tag;
        _roomRootsLeft++;
        _plants.Remove(chosen);
        foreach (Plant plant in _plants.Where(plant => plant.Mature).ToArray())
            HarvestPlant(plant);
        _plants.Clear();
        RefreshRecipes();
        ShowMessage($"留下{DesignNames.Root(tag)}根忆，相邻房间已改变");
    }

    private void HarvestPlant(Plant plant)
    {
        _effects.Add(new Fx { Atlas = "atlas.ecology", Row = 2, Position = plant.Position, Size = 54 });
        if (plant.Type == SeedType.Pea)
        {
            for (int i = 0; i < 5; i++)
                SpawnProjectile(plant.Position, Vector2.Right.Rotated(Mathf.Tau * i / 5) * 175, 6, true, 0, 3);
        }
        else if (plant.Type == SeedType.Chili)
        {
            foreach (Enemy enemy in _enemies.Where(enemy => enemy.Position.DistanceTo(plant.Position) < 44).ToArray())
                DamageEnemy(enemy, 28);
        }
        else if (plant.Type == SeedType.Pumpkin)
        {
            _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 0.5f);
            if (_relics.Contains("空心瓜柄"))
            {
                foreach (Enemy enemy in _enemies.Where(enemy => enemy.Position.DistanceTo(plant.Position) < 55).ToArray())
                    DamageEnemy(enemy, 18);
            }
        }
        else
        {
            foreach (Enemy enemy in _enemies.Take(3).ToArray())
                SpawnProjectile(plant.Position, plant.Position.DirectionTo(enemy.Position) * 140, 8, true, 12, 4, true);
        }
        _plants.Remove(plant);
    }

    private void RefreshRecipes()
    {
        foreach (DesignRoom middle in _map.Rooms.Where(room => room.RootTag != RootTag.None))
        {
            foreach (DesignRoom left in middle.Connections.Select(_map.Room).Where(room => room.RootTag != RootTag.None))
            {
                foreach (DesignRoom right in middle.Connections.Select(_map.Room).Where(room => room.RootTag != RootTag.None && room.Id != left.Id))
                {
                    HashSet<RootTag> tags = [left.RootTag, middle.RootTag, right.RootTag];
                    TryRecipe("雨后菌环", tags, RootTag.Wet, RootTag.Spore, RootTag.Attached);
                    TryRecipe("焦土轮作", tags, RootTag.Burning, RootTag.Rooted, RootTag.Harvest);
                    TryRecipe("月镜花圃", tags, RootTag.Moonlit, RootTag.Wet, RootTag.Rooted);
                    TryRecipe("风中种路", tags, RootTag.Spore, RootTag.Rooted, RootTag.Harvest);
                }
            }
        }

        if (_recipes.Contains("风中种路") && !_map.Room(2).Connections.Contains(9))
        {
            _map.Room(2).Connections.Add(9);
            _map.Room(9).Connections.Add(2);
        }
    }

    private void TryRecipe(string name, HashSet<RootTag> tags, params RootTag[] required)
    {
        if (required.All(tags.Contains) && _recipes.Add(name))
        {
            _score += 250;
            ShowMessage($"生态配方完成：{name}");
        }
    }

    private void TryPlant(Vector2 position)
    {
        if (_roomClear || _seedPods <= 0 || !Arena.HasPoint(position)) return;
        if (SoilAt(position) == SoilType.Corrupted)
        {
            ShowMessage("腐化地无法种植，先用晨露圈净化");
            return;
        }
        if (_plants.Count >= 4 + (_relics.Contains("双月种盘") ? 2 : 0))
            HarvestPlant(_plants[0]);
        _seedPods--;
        AddPlant(_seed, position, false);
        _roomPlantsPlaced++;
        _effects.Add(new Fx { Atlas = "atlas.ecology", Row = 0, Position = position, Size = 34 });
    }

    private void AddPlant(SeedType type, Vector2 position, bool mature)
    {
        _plants.Add(new Plant
        {
            Type = type,
            Position = position,
            Growth = mature ? GrowthTime(type) : 0,
            Mature = mature,
            AttackTimer = 0.3f
        });
    }

    private void TryRoll()
    {
        if (_rollCooldown > 0) return;
        Vector2 direction = _playerVelocity.LengthSquared() > 1 ? _playerVelocity.Normalized() : _aimDirection;
        _playerVelocity = direction * 250;
        _rollTime = 0.18f;
        _invulnerability = Math.Max(_invulnerability, 0.18f);
        _rollCooldown = 1.1f * (_contract == "禁翻" ? 1.5f : 1);
        if (_relics.Contains("不漏水的靴子"))
            _soil.Add(new SoilPatch { Type = SoilType.Wet, Position = _playerPosition, Radius = 23 });
    }

    private void UseDewRing()
    {
        if (_dewCooldown > 0) return;
        _dewCooldown = _relics.Contains("旧铜喷头") ? 13 : 12;
        float radius = _relics.Contains("旧铜喷头") ? 60 : 48;
        _soil.RemoveAll(patch => patch.Type == SoilType.Corrupted && patch.Position.DistanceTo(_playerPosition) <= radius);
        _soil.Add(new SoilPatch { Type = SoilType.Wet, Position = _playerPosition, Radius = radius });
        foreach (Enemy enemy in _enemies.Where(enemy => enemy.Position.DistanceTo(_playerPosition) <= radius).ToArray())
            enemy.Velocity += _playerPosition.DirectionTo(enemy.Position) * 90;
        _effects.Add(new Fx { Atlas = "atlas.ecology", Row = 3, Position = _playerPosition, Size = radius * 2 });
    }

    private void DamageEnemy(Enemy enemy, float damage)
    {
        if (!_enemies.Contains(enemy)) return;
        if (_relics.Contains("金色稻壳") && damage < 100 && _rng.Randf() < 0.12f)
        {
            damage *= 1.5f;
            Plant? growing = _plants.Where(plant => !plant.Mature)
                .OrderBy(plant => plant.Position.DistanceSquaredTo(enemy.Position))
                .FirstOrDefault();
            if (growing != null) growing.Growth += 0.45f;
            _effects.Add(new Fx { Atlas = "atlas.impacts", Position = enemy.Position, Row = 2, Size = 38 });
        }
        enemy.Health -= damage;
        enemy.Flash = 1;
        _effects.Add(new Fx { Position = enemy.Position, Row = 0, Size = 28, Life = 0.18f, MaxLife = 0.18f });
        if (enemy.Health > 0) return;
        _enemies.Remove(enemy);
        int drops = enemy.Type == EnemyType.LanternPumpkinKing ? 12 : _rng.RandiRange(1, 2);
        for (int i = 0; i < drops; i++)
            _pickups.Add(new Pickup { AtlasIndex = 0, Position = enemy.Position + new Vector2(_rng.RandfRange(-12, 12), _rng.RandfRange(-8, 8)) });
        _effects.Add(new Fx { Position = enemy.Position, Row = 3, Size = enemy.Type == EnemyType.LanternPumpkinKing ? 96 : 42 });
    }

    private void HurtPlayer(Vector2 source, float damage)
    {
        if (_invulnerability > 0 || _rollTime > 0) return;
        damage *= _difficulty switch { 0 => 0.75f, 2 => 1.35f, _ => 1 };
        _playerHealth -= damage;
        _roomDamageTaken++;
        _invulnerability = 0.8f;
        _playerVelocity += source.DirectionTo(_playerPosition) * 100;
        if (_playerHealth <= 0)
        {
            _playerHealth = 0;
            _screen = GameScreen.Result;
            PlayMusic("res://assets/audio/music/menu.ogg");
        }
    }

    private void SpawnProjectile(Vector2 position, Vector2 velocity, float damage, bool friendly, int atlasIndex, float radius, bool homing = false, bool heavy = false, float life = 3)
    {
        _projectiles.Add(new Projectile
        {
            Position = position,
            Velocity = velocity,
            Damage = damage,
            Friendly = friendly,
            AtlasIndex = atlasIndex,
            Radius = radius,
            Homing = homing,
            Heavy = heavy,
            Life = life
        });
    }

    private Enemy? NearestEnemy(Vector2 position) =>
        _enemies.OrderBy(enemy => enemy.Position.DistanceSquaredTo(position)).FirstOrDefault();

    private SoilType SoilAt(Vector2 position) =>
        _soil.Where(patch => patch.Position.DistanceTo(position) <= patch.Radius)
            .OrderByDescending(patch => patch.Type == SoilType.Corrupted)
            .Select(patch => patch.Type)
            .FirstOrDefault();

    private static float GrowthTime(SeedType seed) => seed switch
    {
        SeedType.Chili => 4,
        SeedType.Pumpkin => 5.5f,
        SeedType.Dandelion => 3.5f,
        _ => 3
    };

    private RootTag RootTagFor(Plant plant)
    {
        SoilType soil = SoilAt(plant.Position);
        if (soil == SoilType.Wet) return RootTag.Wet;
        if (soil == SoilType.Moonlit) return RootTag.Moonlit;
        return plant.Type switch
        {
            SeedType.Chili => RootTag.Burning,
            SeedType.Pumpkin => RootTag.Rooted,
            SeedType.Dandelion => RootTag.Spore,
            _ => RootTag.Attached
        };
    }

    private void OpenMap()
    {
        _routeIndex = 0;
        _screen = GameScreen.Map;
    }

    private List<DesignRoom> TravelOptions() => _map.Adjacent()
        .Where(room => room.Type != RoomType.Hidden || _map.Current.Type == RoomType.Elite)
        .OrderBy(room => room.Cleared)
        .ThenBy(room => room.Id)
        .ToList();

    private void TravelSelected()
    {
        List<DesignRoom> routes = TravelOptions();
        if (routes.Count == 0) return;
        _routeIndex = Mathf.Clamp(_routeIndex, 0, routes.Count - 1);
        EnterRoom(routes[_routeIndex].Id);
    }

    private void CycleContract()
    {
        List<DesignRoom> routes = TravelOptions();
        if (routes.Count == 0) return;
        DesignRoom target = routes[Mathf.Clamp(_routeIndex, 0, routes.Count - 1)];
        if (target.Cleared || target.Type is not (RoomType.Combat or RoomType.Elite)) return;
        string[] contracts = ["", "虫潮", "旱季", "禁翻", "腐土", "精英授粉"];
        int index = Array.IndexOf(contracts, target.Contract);
        target.Contract = contracts[(index + 1) % contracts.Length];
    }

    private void OpenReward()
    {
        _choiceIndex = 0;
        _screen = GameScreen.Reward;
    }

    private string[] RewardChoices()
    {
        string[] pool =
        [
            "雨后豆荚", "旧铜喷头", "暖手石", "空心瓜柄",
            "蜗牛时钟", "三齿小耙", "月下玻璃瓶", "不漏水的靴子",
            "园丁的便签", "金色稻壳", "双月种盘", "倒栽花盆"
        ];
        Random random = new(CurrentRoom.RewardSeed);
        return pool.OrderBy(_ => random.Next()).Where(name => !_relics.Contains(name)).Take(3).ToArray();
    }

    private void ApplyRelic(string relic)
    {
        if (relic == "三齿小耙") _maxSeedPods++;
        if (relic == "金色稻壳") _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 1);
    }

    private void ResolveEvent()
    {
        CurrentRoom.RewardClaimed = true;
        if ((CurrentRoom.RewardSeed & 1) == 0)
        {
            _moonDew += 8;
            ShowMessage("井底回声：带走这些月露，别让根听见。");
        }
        else
        {
            _playerHealth = Math.Min(_playerMaxHealth, _playerHealth + 2);
            ShowMessage("苔婆婆留下的心莓恢复了 2 点生命。");
        }
    }

    private void CycleWeapon(int direction)
    {
        int count = Enum.GetValues<WeaponType>().Length;
        _weapon = (WeaponType)Wrap((int)_weapon + direction, count);
    }

    private void CycleSeed(int direction)
    {
        int count = Enum.GetValues<SeedType>().Length;
        _seed = (SeedType)Wrap((int)_seed + direction, count);
    }

    private static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;

    private string DifficultyName() => _difficulty switch
    {
        0 => "简单",
        2 => "困难（参数入口）",
        _ => "普通"
    };

    private static float ContractMultiplier(string contract) => contract switch
    {
        "虫潮" => 0.05f,
        "旱季" => 0.08f,
        "禁翻" => 0.10f,
        "腐土" => 0.10f,
        "精英授粉" => 0.15f,
        _ => 0
    };

    private void HandleClick(Vector2 position)
    {
        if (_screen == GameScreen.Title)
        {
            _screen = GameScreen.Loadout;
            return;
        }
        if (_screen == GameScreen.Loadout)
        {
            BeginRun();
            return;
        }
        if (_screen == GameScreen.Map)
        {
            List<DesignRoom> routes = TravelOptions();
            for (int i = 0; i < routes.Count; i++)
            {
                if (RouteCard(i, routes.Count).HasPoint(position))
                {
                    _routeIndex = i;
                    TravelSelected();
                    return;
                }
            }
        }
        if (_screen is GameScreen.HarvestChoice or GameScreen.Reward or GameScreen.Shop or GameScreen.Greenhouse)
        {
            for (int i = 0; i < 3; i++)
                if (ChoiceCard(i).HasPoint(position)) ResolveChoice(i);
        }
    }

    private void ShowMessage(string message)
    {
        _message = message;
        _messageTimer = 3.2f;
    }

    private void InitializeMusic()
    {
        _music = new AudioStreamPlayer { VolumeDb = -14 };
        AddChild(_music);
    }

    private void PlayMusic(string path)
    {
        if (_music == null) return;
        AudioStream? stream = GD.Load<AudioStream>(path);
        if (stream == null) return;
        _music.Stop();
        _music.Stream = stream;
        _music.Play();
    }

    private void UpdateAutomation(float dt)
    {
        _automationTimer += dt;
        if (_screen == GameScreen.Playing && _enemies.Count > 0)
        {
            Enemy target = _enemies[0];
            _aimDirection = _playerPosition.DirectionTo(target.Position);
            DamageEnemy(target, target.MaxHealth + 1);
        }
        if (_screen == GameScreen.Playing && _roomClear)
            ResolveRoomExit();
        if (_screen == GameScreen.HarvestChoice)
            ResolveChoice(_map.CurrentRoomId % 2);
        if (_screen == GameScreen.Reward)
            ResolveChoice(0);
        if (_screen == GameScreen.Map)
        {
            List<DesignRoom> routes = TravelOptions();
            int nextRoomId = FindAutomationNextRoom();
            int next = routes.FindIndex(room => room.Id == nextRoomId);
            _routeIndex = next >= 0 ? next : 0;
            TravelSelected();
        }
        if (_screen == GameScreen.Shop || _screen == GameScreen.Greenhouse)
            OpenMap();
        if (_screen == GameScreen.Result && _bossDefeated)
        {
            GD.Print($"DESIGN_SMOKE_TEST_OK rooms={_roomsCleared} boss={_bossDefeated} score={_score} map_nodes={_map.Rooms.Count}");
            GetTree().Quit(0);
            _automation = false;
        }
        if (_automationTimer > 30)
        {
            GD.Print($"DESIGN_SMOKE_TEST_FAILED screen={_screen} room={_map.CurrentRoomId}");
            GetTree().Quit(1);
            _automation = false;
        }
    }

    private int FindAutomationNextRoom()
    {
        HashSet<int> targets = _map.Rooms
            .Where(room => !room.Cleared && room.Type != RoomType.Boss)
            .Select(room => room.Id)
            .ToHashSet();
        if (targets.Count == 0)
            targets.Add(_map.Rooms.First(room => room.Type == RoomType.Boss).Id);

        Queue<(int RoomId, int FirstStep)> frontier = new();
        HashSet<int> visited = [_map.CurrentRoomId];
        foreach (int neighbor in CurrentRoom.Connections)
        {
            frontier.Enqueue((neighbor, neighbor));
            visited.Add(neighbor);
        }

        while (frontier.Count > 0)
        {
            (int roomId, int firstStep) = frontier.Dequeue();
            if (targets.Contains(roomId))
                return firstStep;
            foreach (int neighbor in _map.Room(roomId).Connections)
            {
                if (visited.Add(neighbor))
                    frontier.Enqueue((neighbor, firstStep));
            }
        }
        return CurrentRoom.Connections.FirstOrDefault();
    }

    public override void _Draw()
    {
        switch (_screen)
        {
            case GameScreen.Title:
                DrawTitle();
                return;
            case GameScreen.Loadout:
                DrawLoadout();
                return;
        }

        DrawWorld();
        DrawHud();

        if (_screen == GameScreen.Map) DrawMap();
        if (_screen == GameScreen.HarvestChoice) DrawHarvestChoice();
        if (_screen == GameScreen.Reward) DrawReward();
        if (_screen == GameScreen.Shop) DrawShop();
        if (_screen == GameScreen.Greenhouse) DrawGreenhouse();
        if (_screen == GameScreen.Pause) DrawPause();
        if (_screen == GameScreen.Result) DrawResult();

        if (_messageTimer > 0)
        {
            DrawMenuPanel(new Rect2(70, 226, 340, 34), 1);
            Text(_message, new Vector2(80, 248), 10, Parchment, 320, HorizontalAlignment.Center);
        }
    }

    private void DrawTitle()
    {
        DrawTextureRect(_assets["title"], new Rect2(0, 0, Width, Height), false, new Color(1, 1, 1, 0.9f));
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.24f));
        DrawMenuPanel(new Rect2(92, 31, 296, 203), 0);
        Text("月 根 秘 境", new Vector2(110, 87), 28, Parchment, 260, HorizontalAlignment.Center);
        Text("MOONROOT HOLLOW", new Vector2(110, 108), 9, Cyan, 260, HorizontalAlignment.Center);
        DrawMenuButton(new Rect2(155, 136, 170, 36), true);
        Text("开始新旅程", new Vector2(155, 160), 14, Parchment, 170, HorizontalAlignment.Center);
        DrawMenuButton(new Rect2(155, 178, 170, 32), false);
        Text("继续游戏", new Vector2(155, 200), 12, new Color(Parchment, 0.52f), 170, HorizontalAlignment.Center);
        Text("Enter / 点击", new Vector2(0, 249), 10, Parchment, Width, HorizontalAlignment.Center);
        DrawGridCell("atlas.npcs", 2, 2, 0, 0, new Rect2(25, 157, 82, 82), new Color(1, 1, 1, 0.78f));
        DrawGridCell("atlas.npcs", 2, 2, 1, 1, new Rect2(374, 158, 80, 80), new Color(1, 1, 1, 0.78f));
    }

    private void DrawLoadout()
    {
        DrawTextureRect(_assets["room.greenhouse"], new Rect2(0, 0, Width, Height), false, new Color(0.7f, 0.76f, 0.82f));
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.46f));
        DrawMenuPanel(new Rect2(50, 24, 380, 220), 0);
        Text("出发前的守圃准备", new Vector2(70, 58), 18, Parchment, 340, HorizontalAlignment.Center);
        DrawCharacter("player", new Vector2(120, 78), 100, 118);
        DrawWeaponSprite(_weapon, new Vector2(270, 89), 74);
        DrawPlantCell(_seed, 3, new Rect2(329, 99, 64, 64));
        DrawGridCell("atlas.npcs", 2, 2, 0, 0, new Rect2(55, 138, 72, 72));
        DrawGridCell("atlas.challenge", 4, 4, _difficulty, 0, new Rect2(366, 35, 48, 48));
        Text($"A / D  主工具：{DesignNames.Weapon(_weapon)}", new Vector2(218, 181), 11, Cyan);
        Text($"W / S  主种子：{DesignNames.Seed(_seed)}", new Vector2(218, 201), 11, Sprout);
        Text($"K  难度：{DifficultyName()}", new Vector2(300, 68), 8, Honey);
        Text("莱芽 · 耐心栽培 / 晨露圈", new Vector2(80, 222), 10, Parchment);
        Text("Enter 开始下潜", new Vector2(265, 226), 11, Honey);
    }

    private void DrawWorld()
    {
        string background = CurrentRoom.Type switch
        {
            RoomType.Boss => "room.boss",
            RoomType.Shop => "room.shop",
            RoomType.Greenhouse => "room.greenhouse",
            _ => "room.combat"
        };
        DrawTextureRect(_assets[background], new Rect2(0, 0, Width, Height), false);
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, CurrentRoom.Type == RoomType.Boss && _enemies.Any(enemy => enemy.Health / enemy.MaxHealth < 0.65f) ? 0.30f : 0.08f));

        foreach (SoilPatch patch in _soil) DrawSoil(patch);
        foreach (Trap trap in _traps) DrawTrap(trap);
        foreach (Plant plant in _plants) DrawPlant(plant);
        foreach (Pickup pickup in _pickups) DrawPickup(pickup);
        foreach (Enemy enemy in _enemies) DrawEnemy(enemy);
        foreach (Projectile projectile in _projectiles) DrawProjectile(projectile);
        DrawWeapon();
        DrawPlayer();
        foreach (Fx effect in _effects) DrawEffect(effect);

        if (_roomClear)
            DrawInteractCell(2, 2, new Rect2(216, 42, 48, 48), new Color(1, 1, 1, 0.88f + Mathf.Sin(_time * 5) * 0.1f));
    }

    private void DrawPlayer()
    {
        float alpha = _invulnerability > 0 && Mathf.Sin(_time * 34) > 0 ? 0.42f : 1;
        Rect2 rect = new(_playerPosition.X - 31, _playerPosition.Y - 43 + Mathf.Sin(_time * 8) * 0.8f, 62, 62);
        DrawTextureRect(_assets["player"], rect, false, new Color(1, 1, 1, alpha));
    }

    private void DrawWeapon()
    {
        int direction = DirectionIndex(_aimDirection);
        int row = (int)_weapon;
        Rect2 rect = new(_playerPosition.X - 22 + _aimDirection.X * 9, _playerPosition.Y - 28 + _aimDirection.Y * 7, 44, 44);
        DrawGridCell("atlas.weapons", 4, 3, direction, row, rect);
        if (_weapon == WeaponType.MoonSickle && _weaponEffect > 0)
        {
            int frame = Mathf.Clamp(3 - Mathf.FloorToInt(_weaponEffect / 0.22f * 4), 0, 3);
            DrawGridCell("atlas.melee", 4, 4, frame, 0, new Rect2(_playerPosition.X - 48, _playerPosition.Y - 48, 96, 96));
        }
        if (_weapon == WeaponType.SunWaterer && _weaponEffect > 0)
        {
            Vector2 end = _playerPosition + _aimDirection * 190;
            float angle = _aimDirection.Angle();
            DrawSetTransform(_playerPosition, angle, Vector2.One);
            DrawGridCell("atlas.laser", 4, 4, Mathf.FloorToInt(_time * 12) % 4, 0, new Rect2(0, -13, 190, 26), new Color(1, 1, 1, 0.92f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private void DrawTrap(Trap trap)
    {
        int state = trap.Cooldown < 0.45f ? 2 : trap.Cooldown < 1.1f ? 1 : 0;
        float pulse = state == 2 ? 1f + Mathf.Sin(_time * 14) * 0.08f : 1f;
        float size = 46 * pulse;
        DrawGridCell("atlas.traps", 4, 4, state, trap.Kind,
            new Rect2(trap.Position.X - size / 2, trap.Position.Y - size / 2, size, size));
    }

    private void DrawEnemy(Enemy enemy)
    {
        string key = enemy.Type switch
        {
            EnemyType.SpikeRadish => "enemy.radish",
            EnemyType.ShellBeetle => "enemy.beetle",
            EnemyType.SeedThief => "enemy.thief",
            EnemyType.EliteRadish => "enemy.elite",
            EnemyType.LanternPumpkinKing => "boss.spring",
            _ => "enemy.sprout"
        };
        float size = enemy.Type switch
        {
            EnemyType.EliteRadish => 84,
            EnemyType.LanternPumpkinKing => 188,
            EnemyType.ShellBeetle => 78,
            _ => 70
        };
        float bob = Mathf.Sin(enemy.Phase * 5) * 1.2f;
        if (enemy.Type == EnemyType.LanternPumpkinKing && enemy.Health / enemy.MaxHealth < 0.3f)
        {
            for (int i = -1; i <= 1; i++)
            {
                float splitSize = 118;
                Vector2 splitPosition = enemy.Position + new Vector2(i * 68, Math.Abs(i) * 15);
                Rect2 splitRect = new(splitPosition.X - splitSize / 2, splitPosition.Y - splitSize * 0.62f + bob, splitSize, splitSize);
                DrawTextureRect(_assets[key], splitRect, false, enemy.Flash > 0 ? new Color(1, 0.72f, 0.72f) : Colors.White);
            }
            return;
        }
        Rect2 rect = new(enemy.Position.X - size / 2, enemy.Position.Y - size * 0.62f + bob, size, size);
        Color modulate = enemy.Flash > 0 ? new Color(1, 0.72f, 0.72f) : Colors.White;
        DrawTextureRect(_assets[key], rect, false, modulate);
        if (enemy.Type == EnemyType.ShellBeetle && enemy.Charging)
            DrawGridCell("atlas.telegraph", 4, 4, 2, 2, new Rect2(enemy.Position.X - 30, enemy.Position.Y - 30, 60, 60), new Color(1, 0.65f, 0.45f, 0.8f));
    }

    private void DrawPlant(Plant plant)
    {
        int stage = plant.Mature ? 3 : Mathf.Clamp((int)(plant.Growth / GrowthTime(plant.Type) * 3), 0, 2);
        DrawPlantCell(plant.Type, stage, new Rect2(plant.Position.X - 25, plant.Position.Y - 31, 50, 50));
        if (plant.Mature)
            DrawArc(plant.Position, 17 + Mathf.Sin(_time * 5), 0, Mathf.Tau, 20, new Color(Honey, 0.65f), 1.5f);
    }

    private void DrawProjectile(Projectile projectile)
    {
        int column = projectile.AtlasIndex % 4;
        int row = projectile.AtlasIndex / 4;
        float size = projectile.Heavy ? 30 : projectile.Friendly ? 22 : 25;
        Color modulate = projectile.Friendly ? Colors.White : new Color(1, 0.72f, 0.68f);
        DrawGridCell("atlas.projectiles", 4, 4, column, row, new Rect2(projectile.Position.X - size / 2, projectile.Position.Y - size / 2, size, size), modulate);
    }

    private void DrawPickup(Pickup pickup)
    {
        int column = pickup.AtlasIndex % 4;
        int row = pickup.AtlasIndex / 4;
        Vector2 position = pickup.Position + new Vector2(0, Mathf.Sin(_time * 5 + pickup.Position.X) * 2);
        DrawGridCell("atlas.pickups", 4, 4, column, row, new Rect2(position.X - 19, position.Y - 19, 38, 38));
    }

    private void DrawEffect(Fx effect)
    {
        int frame = Mathf.Clamp(3 - Mathf.CeilToInt(effect.Life / effect.MaxLife * 4), 0, 3);
        float alpha = Mathf.Clamp(effect.Life / effect.MaxLife, 0.15f, 1);
        DrawGridCell(effect.Atlas, 4, 4, frame, effect.Row, new Rect2(effect.Position.X - effect.Size / 2, effect.Position.Y - effect.Size / 2, effect.Size, effect.Size), new Color(1, 1, 1, alpha));
    }

    private void DrawSoil(SoilPatch patch)
    {
        Color color = patch.Type switch
        {
            SoilType.Wet => new Color(Cyan, 0.24f),
            SoilType.Fertile => new Color(Honey, 0.22f),
            SoilType.Moonlit => new Color(MoonViolet, 0.24f),
            SoilType.Corrupted => new Color(0.55f, 0.12f, 0.36f, 0.30f),
            _ => new Color(SoilBrown, 0.2f)
        };
        DrawCircle(patch.Position, patch.Radius, color);
        int networkIndex = patch.Type switch
        {
            SoilType.Wet => 0,
            SoilType.Moonlit => 4,
            SoilType.Corrupted => 5,
            _ => 3
        };
        DrawGridCell("atlas.network", 4, 4, networkIndex % 4, networkIndex / 4, new Rect2(patch.Position.X - 21, patch.Position.Y - 21, 42, 42), new Color(1, 1, 1, 0.35f));
    }

    private void DrawHud()
    {
        DrawHudRegion(new Rect2(7, 5, 42, 42), new Rect2(55, 35, 235, 220));
        DrawHudRegion(new Rect2(45, 6, 142, 31), new Rect2(370, 55, 550, 125));
        DrawHudRegion(new Rect2(50, 11, 132 * Mathf.Clamp(_playerHealth / _playerMaxHealth, 0, 1), 18), new Rect2(995, 72, 480 * Mathf.Clamp(_playerHealth / _playerMaxHealth, 0, 1), 90));
        DrawCharacter("player", new Vector2(7, 3), 44, 44);
        Text($"{_playerHealth:0.0}/{_playerMaxHealth:0}", new Vector2(65, 27), 9, Parchment);

        DrawHudRegion(new Rect2(190, 5, 150, 34), new Rect2(60, 405, 1420, 130));
        Text(CurrentRoom.Type == RoomType.Boss ? "灯笼南瓜王" : $"春·苔灯地窖  {DesignNames.Room(CurrentRoom.Type)}", new Vector2(195, 22), 9, Parchment, 140, HorizontalAlignment.Center);
        Text($"{DesignNames.Weather(CurrentRoom.Weather)} · 房间 {CurrentRoom.Id:00}", new Vector2(195, 34), 7, Cyan, 140, HorizontalAlignment.Center);

        DrawHudRegion(new Rect2(352, 5, 120, 34), new Rect2(1090, 585, 360, 135));
        DrawGridCell("atlas.icons", 4, 4, 3, 0, new Rect2(359, 9, 24, 24));
        Text($"{_moonDew:00}", new Vector2(382, 26), 10, Honey);
        DrawGridCell("atlas.icons", 4, 4, 1, 1, new Rect2(411, 9, 24, 24));
        Text($"{_seedPods}/{_maxSeedPods}", new Vector2(435, 26), 9, Sprout);

        DrawHudRegion(new Rect2(7, 221, 190, 44), new Rect2(55, 550, 560, 205));
        DrawWeaponSprite(_weapon, new Vector2(13, 220), 42);
        Text(DesignNames.Weapon(_weapon), new Vector2(58, 242), 9, Parchment);
        Text("R 切换", new Vector2(58, 255), 7, Cyan);

        DrawHudRegion(new Rect2(370, 221, 103, 44), new Rect2(710, 545, 300, 210));
        DrawPlantCell(_seed, 3, new Rect2(371, 221, 43, 43));
        Text(DesignNames.Seed(_seed), new Vector2(413, 243), 8, Parchment);
        Text($"Q {Math.Max(0, _dewCooldown):0.0}s", new Vector2(413, 255), 7, Cyan);

        Enemy? boss = _enemies.FirstOrDefault(enemy => enemy.Type == EnemyType.LanternPumpkinKing);
        if (boss != null)
        {
            DrawHudRegion(new Rect2(106, 41, 268, 25), new Rect2(55, 400, 1430, 145));
            DrawRect(new Rect2(119, 51, 242 * Mathf.Clamp(boss.Health / boss.MaxHealth, 0, 1), 7), Pumpkin);
            Text("灯笼南瓜王", new Vector2(110, 64), 8, Parchment, 260, HorizontalAlignment.Center);
        }
    }

    private void DrawMap()
    {
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.91f));
        DrawMenuPanel(new Rect2(17, 14, 446, 238), 0);
        Text("月根网络 · 春季示意地图", new Vector2(28, 38), 14, Parchment);
        Text("Boss 需抵达相邻房后揭示 · K 为目标房选择契约", new Vector2(214, 36), 8, Cyan);

        foreach (DesignRoom room in _map.Rooms)
        {
            foreach (int neighborId in room.Connections.Where(id => id > room.Id))
            {
                DesignRoom neighbor = _map.Room(neighborId);
                Vector2 a = MapPosition(room);
                Vector2 b = MapPosition(neighbor);
                bool rooted = room.RootTag != RootTag.None && neighbor.RootTag != RootTag.None;
                DrawLine(a, b, rooted ? Cyan : new Color(Wood, 0.65f), rooted ? 5 : 2);
            }
        }

        foreach (DesignRoom room in _map.Rooms)
        {
            bool visible = room.Discovered || room.Id == _map.CurrentRoomId || _map.Rooms.Any(discovered => discovered.Discovered && discovered.Connections.Contains(room.Id));
            if (!visible) continue;
            Vector2 p = MapPosition(room);
            bool hiddenBoss = room.Type == RoomType.Boss && !room.BossRevealed;
            int icon = room.RootTag != RootTag.None ? NetworkIcon(room.RootTag) : room.Cleared ? 9 : 8;
            DrawGridCell("atlas.network", 4, 4, icon % 4, icon / 4, new Rect2(p.X - 22, p.Y - 22, 44, 44), room.Id == _map.CurrentRoomId ? Colors.White : new Color(1, 1, 1, 0.78f));
            string label = hiddenBoss ? "未知" : DesignNames.Room(room.Type);
            Text(label, new Vector2(p.X - 27, p.Y + 24), 7, room.Id == _map.CurrentRoomId ? Honey : Parchment, 54, HorizontalAlignment.Center);
            if (room.Weather != RoomWeather.Gloom)
            {
                int weatherIcon = room.Weather == RoomWeather.Rain ? 12 : 13;
                DrawGridCell("atlas.network", 4, 4, weatherIcon % 4, weatherIcon / 4, new Rect2(p.X + 10, p.Y - 25, 27, 27));
            }
        }

        Text($"已激活：{(_recipes.Count == 0 ? "尚无生态配方" : string.Join(" / ", _recipes))}", new Vector2(31, 177), 8, _recipes.Count > 0 ? Sprout : Parchment);
        Text("候选：雨后菌环（湿润+孢子+附着）", new Vector2(31, 190), 7, Cyan);

        List<DesignRoom> routes = TravelOptions();
        for (int i = 0; i < routes.Count; i++)
        {
            Rect2 card = RouteCard(i, routes.Count);
            DrawMenuButton(card, i == _routeIndex);
            DesignRoom room = routes[i];
            string type = room.Type == RoomType.Boss && !room.BossRevealed ? "未知根结" : DesignNames.Room(room.Type);
            Text($"{i + 1} {type}", new Vector2(card.Position.X, card.Position.Y + 16), 8, Parchment, card.Size.X, HorizontalAlignment.Center);
            Text($"{DesignNames.Weather(room.Weather)}{(string.IsNullOrEmpty(room.Contract) ? "" : $" · {room.Contract}")}", new Vector2(card.Position.X, card.Position.Y + 29), 7, room.Weather == RoomWeather.Rain ? Cyan : Honey, card.Size.X, HorizontalAlignment.Center);
        }
    }

    private void DrawHarvestChoice()
    {
        DrawOverlayTitle("清房后的培育决策", "短期生存与跨房根网只能选择其一");
        Rect2 harvest = ChoiceCard(0);
        Rect2 root = ChoiceCard(1);
        DrawMenuButton(harvest, true);
        DrawMenuButton(root, false);
        DrawGridCell("atlas.network", 4, 4, 6, 1, new Rect2(harvest.Position.X + 46, harvest.Position.Y + 10, 52, 52));
        DrawGridCell("atlas.network", 4, 4, NetworkIcon(RootTagFor(_plants.First(plant => plant.Mature))) % 4, NetworkIcon(RootTagFor(_plants.First(plant => plant.Mature))) / 4, new Rect2(root.Position.X + 46, root.Position.Y + 10, 52, 52));
        Text("1 立即收割", new Vector2(harvest.Position.X, harvest.Position.Y + 78), 12, Honey, harvest.Size.X, HorizontalAlignment.Center);
        Text("完整触发收割效果与资源", new Vector2(harvest.Position.X, harvest.Position.Y + 96), 8, Parchment, harvest.Size.X, HorizontalAlignment.Center);
        Text("2 留下根系", new Vector2(root.Position.X, root.Position.Y + 78), 12, Cyan, root.Size.X, HorizontalAlignment.Center);
        Text("放弃该株即时收益，影响邻房与 Boss", new Vector2(root.Position.X, root.Position.Y + 96), 8, Parchment, root.Size.X, HorizontalAlignment.Center);
    }

    private void DrawReward()
    {
        DrawOverlayTitle("芽变三选一", "当前标签相关 / 通用生存 / 转型");
        string[] choices = RewardChoices();
        for (int i = 0; i < choices.Length; i++)
        {
            Rect2 card = ChoiceCard(i);
            DrawMenuButton(card, i == _choiceIndex);
            DrawGridCell("atlas.relics", 4, 3, i % 4, i / 4, new Rect2(card.Position.X + 38, card.Position.Y + 9, 68, 54));
            Text($"{i + 1} {choices[i]}", new Vector2(card.Position.X, card.Position.Y + 79), 10, Honey, card.Size.X, HorizontalAlignment.Center);
            Text(RelicDescription(choices[i]), new Vector2(card.Position.X + 7, card.Position.Y + 96), 7, Parchment, card.Size.X - 14, HorizontalAlignment.Center);
        }
    }

    private void DrawShop()
    {
        DrawOverlayTitle("豆包的月露货台", "商品与价格在地图生成时固定，离房不会刷新");
        string[] names = ["恢复 1 点生命", "补充 1 个种荚", RewardChoices()[0]];
        int[] prices = [8, 10, 22];
        for (int i = 0; i < 3; i++)
        {
            Rect2 card = ChoiceCard(i);
            DrawMenuButton(card, false);
            DrawGridCell(i < 2 ? "atlas.pickups" : "atlas.relics", 4, i < 2 ? 4 : 3, i == 0 ? 3 : i == 1 ? 2 : 0, 0, new Rect2(card.Position.X + 42, card.Position.Y + 12, 60, 52));
            Text($"{i + 1} {names[i]}", new Vector2(card.Position.X, card.Position.Y + 81), 9, Parchment, card.Size.X, HorizontalAlignment.Center);
            Text($"◆ {prices[i]}", new Vector2(card.Position.X, card.Position.Y + 101), 9, Honey, card.Size.X, HorizontalAlignment.Center);
        }
    }

    private void DrawGreenhouse()
    {
        DrawOverlayTitle("苔婆婆的温室", $"本关行动点：{(CurrentRoom.RewardClaimed ? "已使用" : "1")}");
        string[] names = ["升级当前主种子", "移植最近根忆", "封存放弃奖励"];
        string[] descriptions = ["强化一个明确行为", "改为月照节点", "下一关商店再次出现"];
        for (int i = 0; i < 3; i++)
        {
            Rect2 card = ChoiceCard(i);
            DrawMenuButton(card, !CurrentRoom.RewardClaimed && i == _choiceIndex);
            DrawGridCell(i == 0 ? "atlas.plants" : i == 1 ? "atlas.network" : "atlas.interact", 4, 4, i == 0 ? 3 : i == 1 ? 0 : 1, i == 0 ? (int)_seed : i == 1 ? 1 : 3, new Rect2(card.Position.X + 42, card.Position.Y + 12, 60, 52));
            Text($"{i + 1} {names[i]}", new Vector2(card.Position.X, card.Position.Y + 81), 9, Parchment, card.Size.X, HorizontalAlignment.Center);
            Text(descriptions[i], new Vector2(card.Position.X, card.Position.Y + 101), 7, Cyan, card.Size.X, HorizontalAlignment.Center);
        }
    }

    private void DrawPause()
    {
        DrawOverlayTitle("本局构筑", "Esc 继续 · Tab 查看月根地图");
        Text($"主工具：{DesignNames.Weapon(_weapon)}", new Vector2(100, 105), 12, Parchment);
        Text($"主种子：{DesignNames.Seed(_seed)}", new Vector2(100, 127), 12, Parchment);
        Text($"遗物：{(_relics.Count == 0 ? "尚无" : string.Join(" / ", _relics.Take(4)))}", new Vector2(100, 149), 9, Honey);
        Text($"生态配方：{(_recipes.Count == 0 ? "尚未形成" : string.Join(" / ", _recipes))}", new Vector2(100, 169), 9, Cyan);
        Text($"有效时间：{TimeSpan.FromSeconds(_playTime):mm\\:ss}", new Vector2(100, 193), 9, Parchment);
    }

    private void DrawResult()
    {
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.88f));
        DrawMenuPanel(new Rect2(65, 28, 350, 214), 0);
        DrawCharacter(_bossDefeated ? "boss.spring" : "player", new Vector2(82, 53), 115, 115);
        Text(_bossDefeated ? "春季根结已恢复" : "莱芽暂时退回营地", new Vector2(200, 77), 17, _bossDefeated ? Honey : Parchment);
        Text($"结果：{(_bossDefeated ? "击败灯笼南瓜王" : "倒在苔灯地窖")}", new Vector2(205, 106), 10, Parchment);
        Text($"探索房间 {_roomsCleared}   留根 {_roomRootsLeft}   配方 {_recipes.Count}", new Vector2(205, 128), 9, Cyan);
        Text($"积分 {_score}   有效时间 {TimeSpan.FromSeconds(_playTime):mm\\:ss}", new Vector2(205, 149), 10, Honey);
        Text("代表性成果", new Vector2(205, 177), 10, Parchment);
        Text(_recipes.Count > 0 ? string.Join(" / ", _recipes) : "荒行：零配方路线", new Vector2(205, 196), 9, Sprout);
        Text("Enter 返回标题", new Vector2(0, 229), 10, Parchment, Width, HorizontalAlignment.Center);
    }

    private void DrawOverlayTitle(string title, string subtitle)
    {
        DrawRect(new Rect2(0, 0, Width, Height), new Color(Deep, 0.78f));
        DrawMenuPanel(new Rect2(27, 18, 426, 229), 0);
        Text(title, new Vector2(44, 52), 16, Parchment, 392, HorizontalAlignment.Center);
        Text(subtitle, new Vector2(44, 70), 8, Cyan, 392, HorizontalAlignment.Center);
    }

    private void DrawMenuPanel(Rect2 destination, int kind)
    {
        Rect2 source = kind == 0 ? new Rect2(40, 35, 680, 575) : new Rect2(780, 65, 480, 235);
        DrawTextureRectRegion(_assets["atlas.menu"], destination, source);
    }

    private void DrawMenuButton(Rect2 destination, bool selected)
    {
        Rect2 source = selected ? new Rect2(835, 670, 620, 125) : new Rect2(835, 520, 620, 125);
        DrawTextureRectRegion(_assets["atlas.menu"], destination, source);
    }

    private void DrawHudRegion(Rect2 destination, Rect2 source)
    {
        DrawTextureRectRegion(_assets["atlas.hud"], destination, source);
    }

    private void DrawGridCell(string key, int columns, int rows, int column, int row, Rect2 destination, Color? modulate = null)
    {
        Texture2D texture = _assets[key];
        float cellWidth = texture.GetWidth() / (float)columns;
        float cellHeight = texture.GetHeight() / (float)rows;
        Rect2 source = new(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
        DrawTextureRectRegion(texture, destination, source, modulate ?? Colors.White);
    }

    private void DrawPlantCell(SeedType seed, int stage, Rect2 destination) =>
        DrawGridCell("atlas.plants", 4, 4, stage, (int)seed, destination);

    private void DrawWeaponSprite(WeaponType weapon, Vector2 position, float size) =>
        DrawGridCell("atlas.weapons", 4, 3, 0, (int)weapon, new Rect2(position.X, position.Y, size, size));

    private void DrawInteractCell(int column, int row, Rect2 destination, Color? modulate = null) =>
        DrawGridCell("atlas.interact", 4, 4, column, row, destination, modulate);

    private void DrawCharacter(string key, Vector2 position, float width, float height) =>
        DrawTextureRect(_assets[key], new Rect2(position, new Vector2(width, height)), false);

    private static int DirectionIndex(Vector2 direction)
    {
        if (Math.Abs(direction.X) > Math.Abs(direction.Y)) return direction.X < 0 ? 1 : 3;
        return direction.Y < 0 ? 2 : 0;
    }

    private static int NetworkIcon(RootTag tag) => tag switch
    {
        RootTag.Wet => 0,
        RootTag.Burning => 1,
        RootTag.Spore => 2,
        RootTag.Rooted => 3,
        RootTag.Moonlit => 4,
        RootTag.Corrupted => 5,
        RootTag.Harvest => 6,
        RootTag.Attached => 7,
        _ => 8
    };

    private static Vector2 MapPosition(DesignRoom room) => new(66 + room.MapX * 78, 35 + room.MapY * 27);

    private static Rect2 RouteCard(int index, int count)
    {
        float width = Math.Min(112, (420f - Math.Max(0, count - 1) * 6) / Math.Max(1, count));
        float total = width * count + Math.Max(0, count - 1) * 6;
        return new Rect2((Width - total) / 2 + index * (width + 6), 205, width, 39);
    }

    private static Rect2 ChoiceCard(int index) => new(35 + index * 139, 82, 132, 130);

    private static string RelicDescription(string relic) => relic switch
    {
        "雨后豆荚" => "湿润地豆弹额外弹射 1 次",
        "旧铜喷头" => "晨露圈半径 +25%，冷却 +1 秒",
        "暖手石" => "燃烧持续时间 +1.5 秒",
        "空心瓜柄" => "瓜墙破碎造成额外伤害",
        "蜗牛时钟" => "植物成长更快，移动略慢",
        "三齿小耙" => "三种植物同收割返还种荚",
        "月下玻璃瓶" => "月照植物强化并增加风险",
        "不漏水的靴子" => "湿地加速，翻滚延伸湿润",
        "园丁的便签" => "每房首株跳过嫩芽阶段",
        "金色稻壳" => "暴击推进最近植物成长",
        "双月种盘" => "植物上限 +2，收割范围降低",
        "倒栽花盆" => "种子优先附着，绽放增强",
        _ => "改变本局的种植与收割循环"
    };

    private void Text(string value, Vector2 position, int size, Color color, float width = -1, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        if (_font == null) return;
        DrawString(_font, position, value, alignment, width, size, color);
    }
}
