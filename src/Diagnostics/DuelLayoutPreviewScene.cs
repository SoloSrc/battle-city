using System;
using System.Linq;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.Data;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Model;
using BattleCity.DuelScene;
using BattleCity.World;
using Godot;
using CardPosition = BattleCity.Duel.Core.Model.Position;

namespace BattleCity.Diagnostics;

/// <summary>Frozen director review fixture: ten monsters and ten Set Spell/Traps, using the production camera, cards and hand HUD.</summary>
public partial class DuelLayoutPreviewScene : Node3D
{
    private int _frame;
    private DuelStaging _staging = null!;
    private CameraRig _rig = null!;
    private bool _initialized;

    public override void _Ready()
    {
        CardArtwork.UseLocalArt = false;
        _staging = GetNode<DuelStaging>("DuelStaging");
        _rig = GetNode<CameraRig>("CameraRig");
        Character player = GetNode<Character>("Player");
        Character opponent = GetNode<Character>("Opponent");
        _staging.Stage(GetNode<EncounterSite>("EncounterSite"), player, opponent);
        player.PlayState(Character.DuelReadyState);
        opponent.PlayState(Character.DuelReadyState);
        player.Disk?.Deploy();
        opponent.Disk?.Deploy();
        _staging.EnterCamera(_rig);
        GameData data = GameData.Load(ProjectSettings.GlobalizePath(Paths.DataRoot));
        var deck = data.Decks["starter"].ToDeck(data.Cards);
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = 7, FirstPlayer = 0 });
        string[] monsters = { "archfiend_soldier", "airknight_parshath", "summoned_skull", "gemini_elf", "skilled_dark_magician" };
        string[] spells = { "mystical_space_typhoon", "mirror_force", "pot_of_greed", "trap_hole", "bottomless_trap_hole" };
        foreach (PlayerState side in engine.State.Players)
        {
            for (int i = 0; i < 5; i++)
            {
                side.MonsterZones[i] = new CardInstance(Guid.NewGuid(), data.Cards[monsters[i]], side.Index)
                { Loc = Location.MonsterZone, ZoneIndex = i, Pos = CardPosition.FaceUpAttack };
                side.SpellTrapZones[i] = new CardInstance(Guid.NewGuid(), data.Cards[spells[i]], side.Index)
                { Loc = Location.SpellTrapZone, ZoneIndex = i, Pos = CardPosition.FaceDown };
            }
        }

        _staging.Bind(engine);
        var session = new DuelSession { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(session);
        session.Begin(engine, new HeuristicAgent(data.Duelists["d1"].Profile, 7));
        var ui = new DuelUi();
        AddChild(ui);
        ui.Bind(session, _staging);
        _initialized = true;
        GD.Print($"DuelLayoutPreview: {engine.State.Players.Sum(p => p.MonsterCount)} monsters, {engine.State.Players.Sum(p => p.SpellTrapCount)} Set Spell/Traps");
    }

    public override void _Process(double delta)
    {
        if (++_frame != 240)
        {
            return;
        }

        int failures = 0;
        if (!_initialized || _staging.Cards.Values.Count(v => v.Card!.IsOnField) != 20)
        {
            GD.PushError("DuelLayoutPreview FAIL: fixture must contain twenty field cards");
            GetTree().Quit(1);
            return;
        }

        foreach (CardView view in _staging.Cards.Values.Where(v => v.Card!.IsOnField))
        {
            Vector2 screen = _rig.Camera!.UnprojectPosition(view.GlobalPosition);
            bool sized = view.Scale.IsEqualApprox(Vector3.One * DuelStaging.FieldCardScale);
            bool picked = _staging.Pick(_rig.Camera, screen) == view;
            if (!sized || !picked || !GetViewport().GetVisibleRect().HasPoint(screen))
            {
                GD.PushError($"DuelLayoutPreview FAIL: {view.Name} size={sized}, picking={picked}, screen={screen}");
                failures++;
            }
        }

        GD.Print($"DuelLayoutPreview: twenty-card size/picking/framing check, {failures} failures");
        string[] args = OS.GetCmdlineUserArgs();
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length)
        {
            GetViewport().GetTexture().GetImage().SavePng(args[capture + 1]);
            GD.Print($"DuelLayoutPreview: captured {args[capture + 1]}");
            GetTree().Quit(failures == 0 ? 0 : 1);
        }
    }
}
